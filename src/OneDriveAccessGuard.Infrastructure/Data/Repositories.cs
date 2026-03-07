using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneDriveAccessGuard.Core.Enums;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;

namespace OneDriveAccessGuard.Infrastructure.Data;

public class SharedItemRepository : ISharedItemRepository
{
    private readonly AccessGuardDbContext _db;

    public SharedItemRepository(AccessGuardDbContext db) => _db = db;

    public async Task UpsertAsync(IEnumerable<SharedItem> items)
    {
        foreach (var item in items)
        {
            var entity = ToEntity(item);
            var existing = await _db.SharedItems.FindAsync(entity.Id);
            if (existing == null)
            {
                _db.SharedItems.Add(entity);
            }
            else
            {
                existing.Name = entity.Name;
                existing.WebUrl = entity.WebUrl;
                existing.OwnerId = entity.OwnerId;
                existing.OwnerDisplayName = entity.OwnerDisplayName;
                existing.OwnerEmail = entity.OwnerEmail;
                existing.SizeBytes = entity.SizeBytes;
                existing.LastModified = entity.LastModified;
                existing.DetectedAt = entity.DetectedAt;
                existing.IsFolder = entity.IsFolder;
                existing.RiskLevel = entity.RiskLevel;
                existing.PermissionsJson = entity.PermissionsJson;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<SharedItem>> GetAllAsync()
    {
        var entities = await _db.SharedItems.ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<IEnumerable<SharedItem>> GetByRiskLevelAsync(RiskLevel minLevel)
    {
        var entities = await _db.SharedItems
            .Where(e => e.RiskLevel >= minLevel)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<SharedItem?> GetByIdAsync(string id)
    {
        var entity = await _db.SharedItems.FindAsync(id);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task DeleteAsync(string id)
    {
        var entity = await _db.SharedItems.FindAsync(id);
        if (entity != null)
        {
            _db.SharedItems.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    public Task<int> GetCountAsync() => _db.SharedItems.CountAsync();

    private static SharedItemEntity ToEntity(SharedItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        WebUrl = item.WebUrl,
        OwnerId = item.OwnerId,
        OwnerDisplayName = item.OwnerDisplayName,
        OwnerEmail = item.OwnerEmail,
        SizeBytes = item.SizeBytes,
        LastModified = item.LastModified,
        DetectedAt = item.DetectedAt,
        IsFolder = item.IsFolder,
        RiskLevel = item.RiskLevel,
        PermissionsJson = JsonSerializer.Serialize(item.Permissions)
    };

    private static SharedItem ToDomain(SharedItemEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        WebUrl = e.WebUrl,
        OwnerId = e.OwnerId,
        OwnerDisplayName = e.OwnerDisplayName,
        OwnerEmail = e.OwnerEmail,
        SizeBytes = e.SizeBytes,
        LastModified = e.LastModified,
        DetectedAt = e.DetectedAt,
        IsFolder = e.IsFolder,
        RiskLevel = e.RiskLevel,
        Permissions = JsonSerializer.Deserialize<List<SharePermission>>(e.PermissionsJson) ?? []
    };
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AccessGuardDbContext _db;

    public AuditLogRepository(AccessGuardDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log)
    {
        _db.AuditLogs.Add(new AuditLogEntity
        {
            Id = log.Id,
            ExecutedAt = log.ExecutedAt,
            ExecutedBy = log.ExecutedBy,
            Action = log.Action,
            TargetItemId = log.TargetItemId,
            TargetItemName = log.TargetItemName,
            PermissionId = log.PermissionId,
            BeforeState = log.BeforeState,
            AfterState = log.AfterState,
            IsSuccess = log.IsSuccess,
            ErrorMessage = log.ErrorMessage
        });
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int count = 100)
    {
        var entities = await _db.AuditLogs
            .OrderByDescending(e => e.ExecutedAt)
            .Take(count)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    public async Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        var entities = await _db.AuditLogs
            .Where(e => e.ExecutedAt >= from && e.ExecutedAt <= to)
            .OrderByDescending(e => e.ExecutedAt)
            .ToListAsync();
        return entities.Select(ToDomain);
    }

    private static AuditLog ToDomain(AuditLogEntity e) => new()
    {
        Id = e.Id,
        ExecutedAt = e.ExecutedAt,
        ExecutedBy = e.ExecutedBy,
        Action = e.Action,
        TargetItemId = e.TargetItemId,
        TargetItemName = e.TargetItemName,
        PermissionId = e.PermissionId,
        BeforeState = e.BeforeState,
        AfterState = e.AfterState,
        IsSuccess = e.IsSuccess,
        ErrorMessage = e.ErrorMessage
    };
}
