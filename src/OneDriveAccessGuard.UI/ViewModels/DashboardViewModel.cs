using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.UI.ViewModels;

public record ExtensionCount(string Extension, int Count);

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISharedItemRepository _repository;
    private readonly IUserScanResultRepository _userScanResultRepository;

    [ObservableProperty] private int _totalSharedFilesCount;
    [ObservableProperty] private int _uniqueOwnersCount;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<string> RecentScanDates { get; } = new();
    public ObservableCollection<ExtensionCount> ExtensionCounts { get; } = new();

    public DashboardViewModel(ISharedItemRepository repository, IUserScanResultRepository userScanResultRepository)
    {
        _repository = repository;
        _userScanResultRepository = userScanResultRepository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = (await _repository.GetAllAsync()).ToList();

            TotalSharedFilesCount = all.Count;
            UniqueOwnersCount = all.Select(x => x.OwnerId).Distinct().Count();

            var dates = await _userScanResultRepository.GetRecentScanDatesAsync(10);
            RecentScanDates.Clear();
            foreach (var d in dates)
                RecentScanDates.Add(d.ToString("yyyy/MM/dd"));

            var extensions = all
                .GroupBy(x => Path.GetExtension(x.Name).ToLowerInvariant() is { Length: > 0 } ext ? ext : "(拡張子なし)")
                .Select(g => new ExtensionCount(g.Key, g.Count()))
                .OrderByDescending(e => e.Count);

            ExtensionCounts.Clear();
            foreach (var e in extensions)
                ExtensionCounts.Add(e);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
