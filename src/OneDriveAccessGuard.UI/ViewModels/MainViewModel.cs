using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly DashboardViewModel _dashboardVm;
    private readonly ScanViewModel _scanVm;
    private readonly SharedItemsViewModel _sharedItemsVm;

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
        IAuthService authService,
        DashboardViewModel dashboardVm,
        ScanViewModel scanVm,
        SharedItemsViewModel sharedItemsVm)
    {
        _authService = authService;
        _dashboardVm = dashboardVm;
        _scanVm = scanVm;
        _sharedItemsVm = sharedItemsVm;
    }

    public async Task InitializeAsync()
    {
        // 認証
        await _authService.SignInAsync();
        SignedInUserName = _authService.SignedInUserName ?? string.Empty;
        SignedInUserEmail = _authService.SignedInUserEmail ?? string.Empty;

        // 初期ページ表示
        SelectedNavItem = NavigationItems[0];
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
            _           => _dashboardVm
        };
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        SelectedNavItem = NavigationItems.First(n => n.Key == "scan");
        await _scanVm.StartScanCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _authService.SignOutAsync();
        SignedInUserName = string.Empty;
        SignedInUserEmail = string.Empty;
    }
}

/// <summary>ナビゲーション項目</summary>
public record NavItem(string Label, string Icon, string Key);
