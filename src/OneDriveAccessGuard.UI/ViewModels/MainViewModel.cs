using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardVm;
    private readonly ScanViewModel _scanVm;
    private readonly SharedItemsViewModel _sharedItemsVm;
    private readonly SettingsViewModel _settingsVm;

    [ObservableProperty]
    private string _signedInUserName = string.Empty;

    [ObservableProperty]
    private string _signedInUserEmail = string.Empty;

    [ObservableProperty]
    private string _currentPageTitle = "ダッシュボード";

    [ObservableProperty]
    private string _lastScanTimeText = "最終スキャン: 未実行";

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public List<NavItem> NavigationItems { get; } =
    [
        new NavItem("ダッシュボード", "ViewDashboard",  "dashboard"),
        new NavItem("スキャン",       "RadarScan",       "scan"),
        new NavItem("公開ファイル",   "FolderAlert",     "files"),
        new NavItem("一括操作",       "PlaylistRemove",  "bulk"),
        new NavItem("レポート",       "ChartBar",        "report"),
        new NavItem("設定",           "Cog",             "settings"),
    ];

    public MainViewModel(
        DashboardViewModel dashboardVm,
        ScanViewModel scanVm,
        SharedItemsViewModel sharedItemsVm,
        SettingsViewModel settingsVm)
    {
        _dashboardVm = dashboardVm;
        _scanVm = scanVm;
        _sharedItemsVm = sharedItemsVm;
        _settingsVm = settingsVm;
    }

    public Task InitializeAsync()
    {
        // 証明書認証はサインイン不要のため、Windowsユーザー名を表示
        SignedInUserName = Environment.UserName;
        SignedInUserEmail = string.Empty;
        SelectedNavItem = NavigationItems[0];
        return Task.CompletedTask;
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value == null) return;

        CurrentPageTitle = value.Label;
        CurrentPage = value.Key switch
        {
            "dashboard" => _dashboardVm,
            "scan"      => _scanVm,
            "files"     => _sharedItemsVm,
            "settings"  => _settingsVm,
            _           => _dashboardVm
        };
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        SelectedNavItem = NavigationItems.First(n => n.Key == "scan");
        await _scanVm.StartScanCommand.ExecuteAsync(null);
    }
}

/// <summary>ナビゲーション項目</summary>
public record NavItem(string Label, string Icon, string Key);
