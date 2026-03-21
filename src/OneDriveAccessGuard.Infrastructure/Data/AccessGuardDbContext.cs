using Microsoft.EntityFrameworkCore;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.Infrastructure.Data;

/// <summary>
/// ローカルSQLiteキャッシュ用DBコンテキスト
/// </summary>
public class AccessGuardDbContext : DbContext
{
    public DbSet<SharedItemEntity> SharedItems => Set<SharedItemEntity>();
    public DbSet<ScanSessionEntity> ScanSessions => Set<ScanSessionEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<UserScanResultEntity> UserScanResults => Set<UserScanResultEntity>();

    public AccessGuardDbContext(DbContextOptions<AccessGuardDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedItemEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.RiskLevel).HasConversion<int>();
            b.HasIndex(e => e.RiskLevel);
            b.HasIndex(e => e.OwnerId);
        });

        modelBuilder.Entity<ScanSessionEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Status).HasConversion<int>();
        });

        modelBuilder.Entity<AuditLogEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.ExecutedAt);
        });

        modelBuilder.Entity<UserScanResultEntity>(b =>
        {
            b.HasKey(e => e.UserId);
        });
    }
}

public class SharedItemEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsFolder { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string PermissionsJson { get; set; } = "[]"; // JSON serialized
}

public class ScanSessionEntity
{
    public Guid Id { get; set; }
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

public class UserScanResultEntity
{
    public string UserId { get; set; } = string.Empty;
    public int RiskFiles { get; set; }
    public int AllFiles { get; set; }
    public DateTime LastCheckDate { get; set; }
}

public class AuditLogEntity
{
    public Guid Id { get; set; }
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
