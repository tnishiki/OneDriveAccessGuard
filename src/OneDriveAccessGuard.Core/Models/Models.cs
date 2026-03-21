using System.ComponentModel;
using System.Runtime.CompilerServices;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.Core.Models;

/// <summary>
/// OneDrive上の共有ファイル/フォルダ情報
/// </summary>
public class SharedItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? CreatedDateTime { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsFolder { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<SharePermission> Permissions { get; set; } = new();
}

/// <summary>
/// 共有アクセス許可情報
/// </summary>
public class SharePermission
{
    public string Id { get; set; } = string.Empty;
    public SharingType SharingType { get; set; }
    public string? ShareLink { get; set; }
    public DateTime? ExpirationDateTime { get; set; }
    public string? GrantedToEmail { get; set; }
    public string? GrantedToDisplayName { get; set; }
    public string Roles { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
}

/// <summary>
/// 組織内ユーザー情報
/// </summary>
public class OrgUser : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public bool IsEnabled { get; set; }

    private int _riskFiles;
    private int _allFiles;
    private DateTime? _lastCheckDate;

    public int RiskFiles
    {
        get => _riskFiles;
        set { _riskFiles = value; OnPropertyChanged(); }
    }

    public int AllFiles
    {
        get => _allFiles;
        set { _allFiles = value; OnPropertyChanged(); }
    }

    public DateTime? LastCheckDate
    {
        get => _lastCheckDate;
        set { _lastCheckDate = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// スキャン実行セッション
/// </summary>
public class ScanSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ScanStatus Status { get; set; }
    public int TotalUsersScanned { get; set; }
    public int TotalItemsFound { get; set; }
    public int HighRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
    public int LowRiskCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 共有変更操作の監査ログ
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ExecutedAt { get; set; }
    public string ExecutedBy { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetItemId { get; set; } = string.Empty;
    public string TargetItemName { get; set; } = string.Empty;
    public string PermissionId { get; set; } = string.Empty;
    public string BeforeState { get; set; } = string.Empty;
    public string AfterState { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// スキャン進捗レポート
/// </summary>
public class ScanProgress
{
    public int ProcessedUsers { get; set; }
    public int TotalUsers { get; set; }
    public string CurrentUserName { get; set; } = string.Empty;
    public int FoundItemsCount { get; set; }

    public double ProgressPercent =>
        TotalUsers == 0 ? 0 : (double)ProcessedUsers / TotalUsers * 100;
}
