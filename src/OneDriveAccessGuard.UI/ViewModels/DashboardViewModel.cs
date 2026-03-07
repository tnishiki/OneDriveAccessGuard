using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISharedItemRepository _repository;

    [ObservableProperty] private int _highRiskCount;
    [ObservableProperty] private int _mediumRiskCount;
    [ObservableProperty] private int _lowRiskCount;
    [ObservableProperty] private int _safeCount;
    [ObservableProperty] private int _totalItemsCount;
    [ObservableProperty] private bool _isLoading;

    public List<SharedItem> RecentHighRiskItems { get; private set; } = [];

    public DashboardViewModel(ISharedItemRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = (await _repository.GetAllAsync()).ToList();
            TotalItemsCount = all.Count;
            HighRiskCount   = all.Count(x => x.RiskLevel == RiskLevel.High);
            MediumRiskCount = all.Count(x => x.RiskLevel == RiskLevel.Medium);
            LowRiskCount    = all.Count(x => x.RiskLevel == RiskLevel.Low);
            SafeCount       = all.Count(x => x.RiskLevel == RiskLevel.Safe);

            RecentHighRiskItems = all
                .Where(x => x.RiskLevel == RiskLevel.High)
                .OrderByDescending(x => x.DetectedAt)
                .Take(10)
                .ToList();

            OnPropertyChanged(nameof(RecentHighRiskItems));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
