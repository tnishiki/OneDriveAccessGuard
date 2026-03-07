namespace OneDriveAccessGuard.Core.Enums;

/// <summary>
/// 共有リスクレベル
/// </summary>
public enum RiskLevel
{
    /// <summary>安全（特定ユーザーのみ）</summary>
    Safe = 0,

    /// <summary>低リスク（組織内全員）</summary>
    Low = 1,

    /// <summary>中リスク（組織外の特定ユーザー）</summary>
    Medium = 2,

    /// <summary>高リスク（匿名リンク・不特定多数）</summary>
    High = 3
}

/// <summary>
/// 共有タイプ
/// </summary>
public enum SharingType
{
    /// <summary>非公開</summary>
    Private,

    /// <summary>特定ユーザーへの招待</summary>
    SpecificPeople,

    /// <summary>組織内リンク</summary>
    OrganizationLink,

    /// <summary>匿名リンク（リンクを知っている全員）</summary>
    AnonymousLink,

    /// <summary>組織外ユーザーへの招待</summary>
    ExternalUser
}

/// <summary>
/// スキャンステータス
/// </summary>
public enum ScanStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}
