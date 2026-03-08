using Microsoft.Graph;
using GraphModels = Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System.Security.Cryptography.X509Certificates;
using CoreModels = OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.Infrastructure.Graph;

/// <summary>
/// Microsoft Graph SDK を使用した OneDrive 共有情報取得サービス
/// </summary>
public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    private static readonly string[] Scopes =
        [
            "https://graph.microsoft.com/.default"
        ];

    public GraphService(string clientId, string tenantId, string thumbprint, ILogger<GraphService> logger)
    {
        _logger = logger;
        var credential = GetClientCertCredential(tenantId, clientId, thumbprint);
        _graphClient = new GraphServiceClient(credential, Scopes);
    }
    private static ClientCertificateCredential GetClientCertCredential(
           string tenantId, string clientId, string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var certificate = store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(cert =>
                string.Equals(cert.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"証明書が見つかりません。Thumbprint: {thumbprint}");

        var options = new ClientCertificateCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        return new ClientCertificateCredential(tenantId, clientId, certificate, options);
    }
    /// <inheritdoc/>
    public async Task<IEnumerable<OrgUser>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = new List<OrgUser>();

        try
        {
            var response = await _graphClient.Users.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "displayName", "mail", "department", "jobTitle", "accountEnabled"];
                config.QueryParameters.Top = 999;
                config.QueryParameters.Filter = "accountEnabled eq true";
            }, ct);

            var pageIterator = PageIterator<GraphModels.User, GraphModels.UserCollectionResponse>.CreatePageIterator(
                _graphClient,
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ユーザー一覧の取得に失敗しました");
            throw;
        }

        return users;
    }

    private readonly Dictionary<string, string> _driveIdCache = new();

    private async Task<string?> GetDriveIdAsync(string userId, CancellationToken ct)
    {
        if (_driveIdCache.TryGetValue(userId, out var cached))
            return cached;

        try
        {
            var drive = await _graphClient.Users[userId].Drive.GetAsync(cancellationToken: ct);
            if (drive?.Id != null)
            {
                _driveIdCache[userId] = drive.Id;
                return drive.Id;
            }
        }
        catch(ServiceException ex)
        {
            _logger.LogWarning(ex, "ユーザー {UserId} のドライブ取得に失敗しました", ex.ResponseHeaders);
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

            // ユーザーのOneDriveルートから共有アイテムを再帰的に取得
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
                ? await _graphClient.Drives[driveId].Items["root"].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "webUrl", "size", "lastModifiedDateTime", "folder", "shared"];
                    config.QueryParameters.Top = 200;
                }, ct)
                : await _graphClient.Drives[driveId].Items[folderId].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "webUrl", "size", "lastModifiedDateTime", "folder", "shared"];
                    config.QueryParameters.Top = 200;
                }, ct);
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 404)
        {
            return; // ドライブが存在しない場合はスキップ
        }

        if (response?.Value == null) return;

        foreach (var item in response.Value)
        {
            ct.ThrowIfCancellationRequested();

            // 共有設定が存在するアイテムのみ処理
            if (item.Shared != null)
            {
                var permissions = await GetPermissionsAsync(driveId, item.Id!, ct);
                if (permissions.Any(p => p.SharingType != SharingType.SpecificPeople))
                {
                    var sharedItem = new SharedItem
                    {
                        Id = item.Id ?? string.Empty,
                        Name = item.Name ?? string.Empty,
                        WebUrl = item.WebUrl ?? string.Empty,
                        OwnerId = userId,
                        SizeBytes = item.Size ?? 0,
                        LastModified = item.LastModifiedDateTime?.UtcDateTime ?? DateTime.UtcNow,
                        DetectedAt = DateTime.UtcNow,
                        IsFolder = item.Folder != null,
                        Permissions = permissions,
                        RiskLevel = CalculateRiskLevel(permissions)
                    };
                    results.Add(sharedItem);
                }
            }

            // フォルダの場合は再帰的にスキャン
            if (item.Folder != null && item.Id != null)
            {
                await ScanDriveFolderAsync(userId, driveId, item.Id, results, progress, ct);
            }
        }
    }

    private async Task<List<SharePermission>> GetPermissionsAsync(
        string driveId, string itemId, CancellationToken ct)
    {
        var result = new List<SharePermission>();

        try
        {
            var perms = await _graphClient.Drives[driveId].Items[itemId].Permissions
                .GetAsync(cancellationToken: ct);

            foreach (var perm in perms?.Value ?? [])
            {
                var sharingType = DetermineSharingType(perm);
                result.Add(new SharePermission
                {
                    Id = perm.Id ?? string.Empty,
                    SharingType = sharingType,
                    ShareLink = perm.Link?.WebUrl,
                    ExpirationDateTime = perm.ExpirationDateTime?.UtcDateTime,
                    GrantedToEmail = perm.GrantedToV2?.User?.AdditionalData
                        .TryGetValue("email", out var email) == true ? email?.ToString() : null,
                    GrantedToDisplayName = perm.GrantedToV2?.User?.DisplayName,
                    Roles = string.Join(", ", perm.Roles ?? []),
                    HasPassword = perm.HasPassword ?? false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "アイテム {ItemId} の権限取得に失敗しました", itemId);
        }

        return result;
    }

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
        if (permissions.Any(p => p.SharingType == SharingType.AnonymousLink))
            return RiskLevel.High;
        if (permissions.Any(p => p.SharingType == SharingType.ExternalUser))
            return RiskLevel.Medium;
        if (permissions.Any(p => p.SharingType == SharingType.OrganizationLink))
            return RiskLevel.Low;
        return RiskLevel.Safe;
    }

    /// <inheritdoc/>
    public async Task<bool> RemovePermissionAsync(
        string userId, string itemId, string permissionId, CancellationToken ct = default)
    {
        try
        {
            var driveId = await GetDriveIdAsync(userId, ct);
            if (driveId == null) return false;

            await _graphClient.Drives[driveId].Items[itemId].Permissions[permissionId]
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

            // API Throttling 対策：リクエスト間に短いウェイトを挟む
            await Task.Delay(100, ct);

            if (await RemovePermissionAsync(target.UserId, target.ItemId, target.PermissionId, ct))
                successCount++;
        }
        return successCount;
    }
}

