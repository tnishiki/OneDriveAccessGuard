using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;
using System.Collections.ObjectModel;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class SharedItemsViewModel : ObservableObject
{
    private readonly IGraphService _graphService;
    private readonly IServiceScopeFactory _scopeFactory;
    private List<SharedItem> _allItems = [];

    [ObservableProperty] private ObservableCollection<SharedItem> _displayItems = [];
    [ObservableProperty] private SharedItem? _selectedItem;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private RiskLevel _filterRiskLevel = RiskLevel.Low;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevokePermissionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevokeAllHighRiskCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevokePermissionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevokeAllHighRiskCommand))]
    private bool _isScanRunning;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public SharedItemsViewModel(IGraphService graphService, IServiceScopeFactory scopeFactory)
    {
        _graphService = graphService;
        _scopeFactory = scopeFactory;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISharedItemRepository>();
            _allItems = (await repository.GetAllAsync()).ToList();
            ApplyFilter();
            StatusMessage = $"{_allItems.Count} 件のアイテムを読み込みました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnFilterRiskLevelChanged(RiskLevel value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allItems
            .Where(x => x.RiskLevel >= FilterRiskLevel)
            .Where(x => string.IsNullOrWhiteSpace(FilterText) ||
                        x.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                        x.OwnerDisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));

        DisplayItems = new ObservableCollection<SharedItem>(filtered);
    }

    private bool CanRevoke() => !IsLoading && !IsScanRunning;

    /// <summary>選択したアイテムの全共有を無効化する</summary>
    [RelayCommand(CanExecute = nameof(CanRevoke))]
    private async Task RevokePermissionsAsync(SharedItem? item)
    {
        if (item == null) return;

        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISharedItemRepository>();
            var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            foreach (var perm in item.Permissions.ToList())
            {
                var success = await _graphService.RemovePermissionAsync(
                    item.OwnerId, item.Id, perm.Id);

                await auditLogRepository.AddAsync(new AuditLog
                {
                    ExecutedAt = DateTime.UtcNow,
                    ExecutedBy = Environment.UserName,
                    Action = "RemovePermission",
                    TargetItemId = item.Id,
                    TargetItemName = item.Name,
                    PermissionId = perm.Id,
                    BeforeState = perm.SharingType.ToString(),
                    AfterState = "Removed",
                    IsSuccess = success
                });
            }

            await repository.DeleteAsync(item.Id);
            _allItems.Remove(item);
            ApplyFilter();
            StatusMessage = $"「{item.Name}」の共有を削除しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>高リスクアイテムを一括無効化する</summary>
    [RelayCommand(CanExecute = nameof(CanRevoke))]
    private async Task RevokeAllHighRiskAsync()
    {
        var highRisk = _allItems.Where(x => x.RiskLevel == RiskLevel.High).ToList();
        foreach (var item in highRisk)
        {
            await RevokePermissionsAsync(item);
        }
        StatusMessage = $"高リスクアイテム {highRisk.Count} 件の共有を削除しました";
    }
}
