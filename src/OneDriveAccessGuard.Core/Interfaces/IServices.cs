using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.Core.Interfaces;

/// <summary>
/// アプリケーション設定の永続化インターフェース
/// </summary>
public interface ISettingsService
{
    string? ClientId { get; set; }
    string? TenantId { get; set; }
    string? CertificateThumbprint { get; set; }

    /// <summary>必須項目がすべて設定済みか</summary>
    bool IsConfigured { get; }

    void Load();
    void Save();
}

/// <summary>
/// Microsoft Graph API との通信インターフェース
/// </summary>
public interface IGraphService
{
    /// <summary>組織内全ユーザーを取得する</summary>
    /// <param name="excludeGuests">true のとき ゲストアカウント (userType=Guest) を除外する</param>
    Task<IEnumerable<OrgUser>> GetAllUsersAsync(bool excludeGuests = false, CancellationToken ct = default);

    /// <summary>指定ユーザーのOneDrive上の共有アイテムを取得する。戻り値は (共有アイテム一覧, ドライブ内全ファイル数) のタプル。</summary>
    Task<(IEnumerable<SharedItem> Items, int TotalFileCount)> GetSharedItemsAsync(string userId, string DisplayName, IProgress<ScanProgress>? progress = null, CancellationToken ct = default);

    /// <summary>指定ファイルの共有アクセス許可を削除する（非公開化）</summary>
    Task<bool> RemovePermissionAsync(string userId, string itemId, string permissionId, CancellationToken ct = default);

    /// <summary>指定ファイルの共有アクセス許可を一括削除する</summary>
    Task<int> RemovePermissionsBatchAsync(IEnumerable<(string UserId, string ItemId, string PermissionId)> targets, CancellationToken ct = default);

    /// <summary>設定変更後にクライアントを再初期化する</summary>
    void ReinitializeClient();

    /// <summary>ログ出力のコールバック（UI への追記などに使用）</summary>
    Action<string>? LogCallback { get; set; }
}

/// <summary>
/// スキャンサービスインターフェース
/// </summary>
public interface IScanService
{
    ScanStatus CurrentStatus { get; }

    Task<ScanSession> RunFullScanAsync(IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
    Task<ScanSession> RunUserScanAsync(IEnumerable<string> userIds, IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
    Task<IEnumerable<ScanSession>> GetScanHistoryAsync();
}

/// <summary>
/// 共有アイテムのリポジトリインターフェース
/// </summary>
public interface ISharedItemRepository
{
    Task UpsertAsync(IEnumerable<SharedItem> items);
    Task<IEnumerable<SharedItem>> GetAllAsync();
    Task<IEnumerable<SharedItem>> GetByRiskLevelAsync(Core.Enums.RiskLevel minLevel);
    Task<SharedItem?> GetByIdAsync(string id);
    Task DeleteAsync(string id);
    Task<int> GetCountAsync();
}

/// <summary>
/// 監査ログのリポジトリインターフェース
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100);
    Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime from, DateTime to);
}

/// <summary>
/// ユーザースキャン結果のリポジトリインターフェース
/// </summary>
public interface IUserScanResultRepository
{
    Task UpsertAsync(string userId, int riskFiles, int allFiles, DateTime lastCheckDate);
    Task<IEnumerable<(string UserId, int RiskFiles, int AllFiles, DateTime LastCheckDate)>> GetAllAsync();
    Task<IEnumerable<DateTime>> GetRecentScanDatesAsync(int count = 10);
}

/// <summary>
/// レポート出力インターフェース
/// </summary>
public interface IReportService
{
    Task ExportToCsvAsync(IEnumerable<SharedItem> items, string filePath);
    Task ExportToExcelAsync(IEnumerable<SharedItem> items, string filePath);
}
