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
    public async Task<IEnumerable<OrgUser>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = new List<OrgUser>();

        try
        {
            var response = await Client.Users.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "displayName", "mail", "department", "jobTitle", "accountEnabled"];
                config.QueryParameters.Top = 999;
                config.QueryParameters.Filter = "accountEnabled eq true";
            }, ct);

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
            _logger.LogInformation("{Count} 人のユーザーを取得しました", users.Count);
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

            await ScanDriveFolderAsync(userId, driveId, "root", sharedItems, progress, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ユーザー {UserId} のスキャン中にエラーが発生しました", userId);
        }

        return sharedItems;
    }

    private async Task ScanDriveFolderAsync(
        string userId,
        string driveId,
        string folderId,
        List<SharedItem> results,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        GraphModels.DriveItemCollectionResponse? response;

        try
        {
            response = folderId == "root"
                ? await Client.Drives[driveId].Items["root"].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "webUrl", "size", "lastModifiedDateTime", "folder", "shared"];
                    config.QueryParameters.Top = 200;
                }, ct)
                : await Client.Drives[driveId].Items[folderId].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "webUrl", "size", "lastModifiedDateTime", "folder", "shared"];
                    config.QueryParameters.Top = 200;
                }, ct);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            return;
        }

        if (response?.Value == null) return;

        foreach (var item in response.Value)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Shared != null)
            {
                var permissions = await GetPermissionsAsync(driveId, item.Id!, ct);
                if (permissions.Any(p => p.SharingType != SharingType.SpecificPeople))
                {
                    results.Add(new SharedItem
                    {
                        Id           = item.Id ?? string.Empty,
                        Name         = item.Name ?? string.Empty,
                        WebUrl       = item.WebUrl ?? string.Empty,
                        OwnerId      = userId,
                        SizeBytes    = item.Size ?? 0,
                        LastModified = item.LastModifiedDateTime?.UtcDateTime ?? DateTime.UtcNow,
                        DetectedAt   = DateTime.UtcNow,
                        IsFolder     = item.Folder != null,
                        Permissions  = permissions,
                        RiskLevel    = CalculateRiskLevel(permissions)
                    });
                }
            }

            if (item.Folder != null && item.Id != null)
                await ScanDriveFolderAsync(userId, driveId, item.Id, results, progress, ct);
        }
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
