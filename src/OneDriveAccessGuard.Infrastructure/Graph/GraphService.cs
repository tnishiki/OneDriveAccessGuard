using Microsoft.Graph;
using GraphModels = Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System.Security.Cryptography.X509Certificates;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;

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
        string? accountFilter = null,
        CancellationToken ct = default)
    {
        var users = new List<OrgUser>();

        try
        {
            var odataFilter = excludeGuests
                ? "accountEnabled eq true and userType eq 'Member'"
                : "accountEnabled eq true";

            var hasAccountFilter = !string.IsNullOrWhiteSpace(accountFilter);

            GraphModels.UserCollectionResponse? response;

            if (hasAccountFilter)
            {
                // $search で displayName / mail の部分一致検索（ConsistencyLevel: eventual が必須）
                // $filter と $search は同時使用可能（$count=true も必要）
                response = await Client.Users.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "displayName", "mail", "department", "jobTitle", "accountEnabled"];
                    config.QueryParameters.Top    = 999;
                    config.QueryParameters.Search = $"\"displayName:{accountFilter}\" OR \"mail:{accountFilter}\"";
                    config.QueryParameters.Filter = odataFilter;
                    config.QueryParameters.Count  = true;
                    config.Headers.Add("ConsistencyLevel", "eventual");
                }, ct);
            }
            else
            {
                response = await Client.Users.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "displayName", "mail", "department", "jobTitle", "accountEnabled"];
                    config.QueryParameters.Top    = 999;
                    config.QueryParameters.Filter = odataFilter;
                }, ct);
            }

            var pageIterator = PageIterator<GraphModels.User, GraphModels.UserCollectionResponse>.CreatePageIterator(
                Client,
                response!,
                user =>
                {
                    users.Add(new OrgUser
                    {
                        Id          = user.Id          ?? string.Empty,
                        DisplayName = user.DisplayName ?? string.Empty,
                        Email       = user.Mail        ?? string.Empty,
                        Department  = user.Department,
                        JobTitle    = user.JobTitle,
                        IsEnabled   = user.AccountEnabled ?? false
                    });
                    return true;
                });

            await pageIterator.IterateAsync(ct);
            _logger.LogInformation(
                hasAccountFilter
                    ? "{Count} 人のユーザーを取得しました（フィルター: {Filter}）"
                    : "{Count} 人のユーザーを取得しました",
                users.Count,
                accountFilter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ユーザー一覧の取得に失敗しました");
            throw;
        }

        return users;
    }

    // ─── 共有アイテムスキャン ────────────────────────────────────────

    private async Task<string?> GetDriveIdAsync(string userId, CancellationToken ct)
    {
        if (_driveIdCache.TryGetValue(userId, out var cached))
            return cached;

        try
        {
            var drive = await Client.Users[userId].Drive.GetAsync(cancellationToken: ct);
            if (drive?.Id != null)
            {
                _driveIdCache[userId] = drive.Id;
                return drive.Id;
            }
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning(ex, "ユーザー {UserId} のドライブ取得に失敗しました", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ユーザー {UserId} のドライブ取得に失敗しました", userId);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SharedItem>> GetSharedItemsAsync(
        string userId,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sharedItems = new List<SharedItem>();

        try
        {
            var driveId = await GetDriveIdAsync(userId, ct);
            if (driveId == null) return sharedItems;

            await ScanDriveWithDeltaAsync(userId, driveId, sharedItems, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ユーザー {UserId} のスキャン中にエラーが発生しました", userId);
        }

        return sharedItems;
    }

    /// <summary>
    /// /delta API でドライブ内の全アイテムをフラットに取得し、共有アイテムを抽出する。
    /// 再帰的フォルダ走査と異なり、1 回のページング処理で全階層を網羅できる。
    /// </summary>
    private async Task ScanDriveWithDeltaAsync(
        string userId,
        string driveId,
        List<SharedItem> results,
        CancellationToken ct)
    {
        // Phase 1: delta で候補収集（変更なし）
        var candidates = new List<GraphModels.DriveItem>();

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
                    if (item.Shared?.Scope == "anonymous" && item.Id != null)
                        candidates.Add(item);  // ← ここでScope絞り込みも同時に行う
                }

                if (page.OdataNextLink == null) break;

                page = await Client.Drives[driveId].Items["root"].Delta
                    .WithUrl(page.OdataNextLink)
                    .GetAsDeltaGetResponseAsync(cancellationToken: ct);
            }
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("ユーザー {UserId} のドライブが見つかりません", userId);
            return;
        }

        _logger.LogDebug("ユーザー {UserId}: {Count} 件の匿名共有候補を検出", userId, candidates.Count);

        if (candidates.Count == 0) return;

        // Phase 2: Permissions APIを並列取得（同時3件でThrottling回避）
        var semaphore = new SemaphoreSlim(3);
        var localResults = new System.Collections.Concurrent.ConcurrentBag<SharedItem>();

        var tasks = candidates.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var permissions = await GetPermissionsAsync(driveId, item.Id!, ct);
                if (permissions.Any(p => p.SharingType == SharingType.AnonymousLink))
                {
                    localResults.Add(new SharedItem
                    {
                        Id = item.Id ?? string.Empty,
                        Name = item.Name ?? string.Empty,
                        WebUrl = permissions[0].ShareLink ?? string.Empty,
                        OwnerId = userId,
                        SizeBytes = item.Size ?? 0,
                        CreatedDateTime = item.CreatedDateTime?.UtcDateTime,
                        LastModified = item.LastModifiedDateTime?.UtcDateTime ?? DateTime.UtcNow,
                        DetectedAt = DateTime.UtcNow,
                        IsFolder = item.Folder != null,
                        Permissions = permissions,
                        RiskLevel = CalculateRiskLevel(permissions)
                    });
                }
            }
            catch (ServiceException ex) when (ex.ResponseStatusCode == 429)
            {
                // Throttling発生時はリトライ
                var retryAfter = ex.ResponseHeaders?
                    .TryGetValues("Retry-After", out var vals) == true
                    ? int.Parse(vals.First()) : 10;
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        foreach (var item in localResults)
            results.Add(item);
    }
    private async Task<List<SharePermission>> GetPermissionsAsync(
        string driveId, string itemId, CancellationToken ct)
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
                    Id                    = perm.Id ?? string.Empty,
                    SharingType           = DetermineSharingType(perm),
                    ShareLink             = perm.Link?.WebUrl,
                    ExpirationDateTime    = perm.ExpirationDateTime?.UtcDateTime,
                    GrantedToEmail        = perm.GrantedToV2?.User?.AdditionalData
                                               .TryGetValue("email", out var email) == true
                                               ? email?.ToString() : null,
                    GrantedToDisplayName  = perm.GrantedToV2?.User?.DisplayName,
                    Roles                 = string.Join(", ", perm.Roles ?? []),
                    HasPassword           = perm.HasPassword ?? false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "アイテム {ItemId} の権限取得に失敗しました", itemId);
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
            var driveId = await GetDriveIdAsync(userId, ct);
            if (driveId == null) return false;

            await Client.Drives[driveId].Items[itemId].Permissions[permissionId]
                .DeleteAsync(cancellationToken: ct);
            _logger.LogInformation("共有を削除しました: User={UserId}, Item={ItemId}, Perm={PermId}",
                userId, itemId, permissionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "共有削除に失敗しました: User={UserId}, Item={ItemId}", userId, itemId);
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
            "anonymous"    => SharingType.AnonymousLink,
            "organization" => SharingType.OrganizationLink,
            _              => SharingType.SpecificPeople
        };
    }

    private static RiskLevel CalculateRiskLevel(List<SharePermission> permissions)
    {
        if (permissions.Any(p => p.SharingType == SharingType.AnonymousLink))  return RiskLevel.High;
        if (permissions.Any(p => p.SharingType == SharingType.ExternalUser))   return RiskLevel.Medium;
        if (permissions.Any(p => p.SharingType == SharingType.OrganizationLink)) return RiskLevel.Low;
        return RiskLevel.Safe;
    }
}
