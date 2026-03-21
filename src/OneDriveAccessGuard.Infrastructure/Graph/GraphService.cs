using Azure.Identity;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using OneDriveAccessGuard.Core.Enums;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using System.Security.Cryptography.X509Certificates;
using GraphModels = Microsoft.Graph.Models;

namespace OneDriveAccessGuard.Infrastructure.Graph;

/// <summary>
/// Microsoft Graph SDK を使用した OneDrive 共有情報取得サービス
/// </summary>
public class GraphService : IGraphService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<GraphService> _logger;

    private GraphServiceClient? _graphClient;
    private readonly Dictionary<string, string> _driveIdCache = new();

    public Action<string>? LogCallback { get; set; }

    private void Callback(string message) => LogCallback?.Invoke(message);

    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    public GraphService(ISettingsService settings, ILogger<GraphService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// 設定が揃っているときだけクライアントを返す。未設定なら例外。
    /// </summary>
    private GraphServiceClient Client
    {
        get
        {
            if (_graphClient != null) return _graphClient;

            if (!_settings.IsConfigured)
                throw new InvalidOperationException(
                    "Azure AD の設定が未完了です。設定画面から ClientId・TenantId・証明書 Thumbprint を入力してください。");

            var credential = BuildCredential(
                _settings.TenantId!,
                _settings.ClientId!,
                _settings.CertificateThumbprint!);

            _graphClient = new GraphServiceClient(credential, Scopes);
            return _graphClient;
        }
    }

    /// <inheritdoc/>
    public void ReinitializeClient()
    {
        _graphClient = null;
        _driveIdCache.Clear();
        _logger.LogInformation("GraphService クライアントをリセットしました");
        Callback("GraphService クライアントをリセットしました");
    }

    private static ClientCertificateCredential BuildCredential(
        string tenantId, string clientId, string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var cert = store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(c => string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"証明書が見つかりません。Thumbprint: {thumbprint}");

        return new ClientCertificateCredential(tenantId, clientId, cert,
            new ClientCertificateCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            });
    }

    // ─── ユーザー一覧 ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<OrgUser>> GetAllUsersAsync(
        bool excludeGuests = false,
        CancellationToken ct = default)
    {
        var users = new List<OrgUser>();

        try
        {
            var odataFilter = excludeGuests
                ? "accountEnabled eq true and userType eq 'Member'"
                : "accountEnabled eq true";

            var response = await Client.Users.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "displayName", "mail", "department", "jobTitle", "accountEnabled"];
                config.QueryParameters.Top = 999;
                config.QueryParameters.Filter = odataFilter;
            }, ct);

            var pageIterator = PageIterator<GraphModels.User, GraphModels.UserCollectionResponse>.CreatePageIterator(
                Client,
                response!,
                user =>
                {
                    users.Add(new OrgUser
                    {
                        Id = user.Id ?? string.Empty,
                        DisplayName = user.DisplayName ?? string.Empty,
                        Email = user.Mail ?? string.Empty,
                        Department = user.Department,
                        JobTitle = user.JobTitle,
                        IsEnabled = user.AccountEnabled ?? false
                    });
                    return true;
                });

            await pageIterator.IterateAsync(ct);
            _logger.LogInformation("{Count} 人のユーザーを取得しました", users.Count);
            Callback($"{users.Count} 人のユーザーを取得しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ユーザー一覧の取得に失敗しました");
            Callback($"[ERROR] ユーザー一覧の取得に失敗しました: {ex.Message}");
            throw;
        }

        return users;
    }

    // ─── 共有アイテムスキャン ────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(IEnumerable<SharedItem> Items, int TotalFileCount)> GetSharedItemsAsync(
        string userId,
        string DisplayName,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sharedItems = new List<SharedItem>();
        int totalFileCount = 0;

        try
        {
            if (!_driveIdCache.TryGetValue(userId, out var driveId))
            {
                var drive = await Client.Users[userId].Drive.GetAsync(cancellationToken: ct);
                if (drive?.Id == null)
                {
                    _logger.LogWarning("ユーザー {DisplayName} のドライブが見つかりません", DisplayName);
                    Callback($"[WARN] ユーザー {DisplayName} のドライブが見つかりません");
                    return (sharedItems, totalFileCount);
                }
                driveId = drive.Id;
                _driveIdCache[userId] = driveId;
            }

            totalFileCount = await ScanDriveWithDeltaAsync(userId, DisplayName, driveId, sharedItems, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ユーザー {DisplayName} のスキャン中にエラーが発生しました", DisplayName);
            Callback($"[WARN] ユーザー {DisplayName} のスキャン中にエラーが発生しました: {ex.Message}");
        }

        return (sharedItems, totalFileCount);
    }

    /// <summary>
    /// /delta API でドライブ内の全アイテムをフラットに取得し、共有アイテムを抽出する。
    /// 再帰的フォルダ走査と異なり、1 回のページング処理で全階層を網羅できる。
    /// </summary>
    private async Task<int> ScanDriveWithDeltaAsync(
        string userId,
        string DisplayName,
        string driveId,
        List<SharedItem> results,
        CancellationToken ct)
    {
        // Phase 1: delta で候補収集（変更なし）
        var candidates = new List<GraphModels.DriveItem>();
        int totalItemCount = 0;

        try
        {
            var page = await Client.Drives[driveId].Items["root"].Delta
                .GetAsDeltaGetResponseAsync(config =>
                {
                    config.QueryParameters.Select =
                    [
                        "id", "name", "webUrl", "size",
                    "createdDateTime", "lastModifiedDateTime",
                    "folder", "shared"
                    ];
                }, ct);

            while (page != null)
            {
                foreach (var item in page.Value ?? [])
                {
                    totalItemCount++;
                    // Delta API の shared フィールドは不正確なことがあるため、
                    // shared が存在する全アイテムを候補とし、正確な判定は Permissions API で行う
                    if (item.Shared != null)
                    {
                        candidates.Add(item);
                    }
                }

                if (page.OdataNextLink == null) break;

                page = await Client.Drives[driveId].Items["root"].Delta
                    .WithUrl(page.OdataNextLink)
                    .GetAsDeltaGetResponseAsync(cancellationToken: ct);
            }
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning($"ユーザー {DisplayName} のドライブが見つかりません");
            Callback($"[WARN] ユーザー {DisplayName} のドライブが見つかりません");
            return 0;
        }

        _logger.LogDebug($"ユーザー {DisplayName}: {candidates.Count} 件の共有候補を検出 (全 {totalItemCount} ファイル)");
        Callback($"ユーザー {DisplayName}: {candidates.Count} 件の共有候補を検出 (全 {totalItemCount} ファイル)");

        if (candidates.Count == 0) return totalItemCount;

        // Phase 2: Permissions API で正確な共有状態を確認する
        foreach (var item in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var permissions = await GetPermissionsAsync(driveId, item.Id!, item.Name!, ct);
                var riskLevel = CalculateRiskLevel(permissions);
                if (riskLevel == RiskLevel.High)
                {
                    var webUrl = permissions.FirstOrDefault(p => p.ShareLink != null)?.ShareLink
                                 ?? item.WebUrl
                                 ?? string.Empty;
                    results.Add(new SharedItem
                    {
                        Id = item.Id ?? string.Empty,
                        Name = item.Name ?? string.Empty,
                        WebUrl = webUrl,
                        OwnerId = userId,
                        SizeBytes = item.Size ?? 0,
                        CreatedDateTime = item.CreatedDateTime?.UtcDateTime,
                        LastModified = item.LastModifiedDateTime?.UtcDateTime ?? DateTime.UtcNow,
                        DetectedAt = DateTime.UtcNow,
                        IsFolder = item.Folder != null,
                        Permissions = permissions,
                        RiskLevel = riskLevel
                    });
                }
            }
            catch (ServiceException ex) when (ex.ResponseStatusCode == 429)
            {
                var retryAfter = ex.ResponseHeaders?
                    .TryGetValues("Retry-After", out var vals) == true
                    ? int.Parse(vals.First()) : 10;
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct);
            }
        }

        return totalItemCount;
    }
    private async Task<List<SharePermission>> GetPermissionsAsync(
        string driveId, string itemId, string Name, CancellationToken ct)
    {
        var result = new List<SharePermission>();

        try
        {
            var perms = await Client.Drives[driveId].Items[itemId].Permissions
                .GetAsync(cancellationToken: ct);

            foreach (var perm in perms?.Value ?? [])
            {
                result.Add(new SharePermission
                {
                    Id = perm.Id ?? string.Empty,
                    SharingType = DetermineSharingType(perm),
                    ShareLink = perm.Link?.WebUrl,
                    ExpirationDateTime = perm.ExpirationDateTime?.UtcDateTime,
                    GrantedToEmail = perm.GrantedToV2?.User?.AdditionalData
                                               .TryGetValue("email", out var email) == true
                                               ? email?.ToString() : null,
                    GrantedToDisplayName = perm.GrantedToV2?.User?.DisplayName,
                    Roles = string.Join(", ", perm.Roles ?? []),
                    HasPassword = perm.HasPassword ?? false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"アイテム {Name} の権限取得に失敗しました: {ex.Message}");
            Callback($"[WARN] アイテム {Name} の権限取得に失敗しました: {ex.Message}");
        }

        return result;
    }

    // ─── 権限削除 ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> RemovePermissionAsync(
        string userId, string itemId, string permissionId, CancellationToken ct = default)
    {
        try
        {
            if (!_driveIdCache.TryGetValue(userId, out var driveId))
            {
                var drive = await Client.Users[userId].Drive.GetAsync(cancellationToken: ct);
                if (drive?.Id == null) return false;
                driveId = drive.Id;
                _driveIdCache[userId] = driveId;
            }

            await Client.Drives[driveId].Items[itemId].Permissions[permissionId]
                .DeleteAsync(cancellationToken: ct);
            _logger.LogInformation("共有を削除しました: User={UserId}, Item={ItemId}, Perm={PermId}",
                userId, itemId, permissionId);
            Callback($"共有を削除しました: User={userId}, Item={itemId}, Perm={permissionId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "共有削除に失敗しました: User={UserId}, Item={ItemId}", userId, itemId);
            Callback($"[ERROR] 共有削除に失敗しました: User={userId}, Item={itemId}: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> RemovePermissionsBatchAsync(
        IEnumerable<(string UserId, string ItemId, string PermissionId)> targets,
        CancellationToken ct = default)
    {
        int successCount = 0;
        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(100, ct); // API Throttling 対策
            if (await RemovePermissionAsync(target.UserId, target.ItemId, target.PermissionId, ct))
                successCount++;
        }
        return successCount;
    }

    // ─── ヘルパー ────────────────────────────────────────────────────

    private static SharingType DetermineSharingType(Microsoft.Graph.Models.Permission perm)
    {
        if (perm.Link == null) return SharingType.SpecificPeople;

        return perm.Link.Scope switch
        {
            "anonymous" => SharingType.AnonymousLink,
            "organization" => SharingType.OrganizationLink,
            _ => SharingType.SpecificPeople
        };
    }

    private static RiskLevel CalculateRiskLevel(List<SharePermission> permissions)
    {
        if (permissions.Any(p => p.SharingType == SharingType.AnonymousLink)) return RiskLevel.High;
        if (permissions.Any(p => p.SharingType == SharingType.ExternalUser)) return RiskLevel.Medium;
        if (permissions.Any(p => p.SharingType == SharingType.OrganizationLink)) return RiskLevel.Low;
        return RiskLevel.Safe;
    }
}
