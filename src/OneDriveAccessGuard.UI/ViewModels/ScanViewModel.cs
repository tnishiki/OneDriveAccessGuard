using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IGraphService _graphService;
    private readonly ISharedItemRepository _repository;
    private readonly IUserScanResultRepository _userScanResultRepository;
    private readonly SharedItemsViewModel _sharedItemsVm;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ScanStatus _scanStatus = ScanStatus.Idle;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressMessage = "スキャン待機中";
    [ObservableProperty] private int _foundItemsCount;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _excludeGuests = true;
    [ObservableProperty] private string _scanLog = string.Empty;
    [ObservableProperty] private bool _showLatestLog;
    [ObservableProperty] private string _filterKeyword = string.Empty;
    public ObservableCollection<SharedItem> ScannedItems { get; } = new();
    public ObservableCollection<OrgUser> Users { get; } = new();
    public List<OrgUser> SelectedUsers { get; } = new();

    private readonly List<OrgUser> _allUsers = new();

    public ScanViewModel(IGraphService graphService, ISharedItemRepository repository, IUserScanResultRepository userScanResultRepository, SharedItemsViewModel sharedItemsVm)
    {
        _graphService = graphService;
        _repository = repository;
        _userScanResultRepository = userScanResultRepository;
        _sharedItemsVm = sharedItemsVm;
        _ = RefreshUsersAsync();
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (ScanStatus == ScanStatus.Running) return;

        _cts = new CancellationTokenSource();
        ScanStatus = ScanStatus.Running;
        CanCancel = true;
        _sharedItemsVm.IsScanRunning = true;
        FoundItemsCount = 0;
        ScanLog = string.Empty;
        ScannedItems.Clear();

        _graphService.LogCallback = msg => ScanLog += msg + Environment.NewLine;

        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressPercent = p.ProgressPercent;
            ProgressMessage = $"スキャン中: {p.CurrentUserName} ({p.ProcessedUsers}/{p.TotalUsers})";
            FoundItemsCount = p.FoundItemsCount;
        });

        var allItems = new List<SharedItem>();
        var scannedOwnerIds = new List<string>();
        try
        {
            var users = SelectedUsers.Count > 0
                ? (IEnumerable<OrgUser>)SelectedUsers
                : await _graphService.GetAllUsersAsync(ExcludeGuests, _cts.Token);
            int processed = 0;
            int total = users.Count();

            foreach (var user in users)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var (items, totalFileCount) = await _graphService.GetSharedItemsAsync(
                    user.Id, user.DisplayName, progress, _cts.Token);
                var itemList = items.ToList();
                foreach (var item in itemList)
                {
                    item.OwnerDisplayName = user.DisplayName;
                    item.OwnerEmail = user.Email;
                    allItems.Add(item);
                    ScannedItems.Add(item);
                }
                var scanDate = DateTime.Now;
                var orgUser = Users.FirstOrDefault(u => u.Id == user.Id);
                if (orgUser != null)
                {
                    orgUser.RiskFiles = itemList.Count;
                    orgUser.AllFiles = totalFileCount;
                    orgUser.LastCheckDate = scanDate;
                }
                scannedOwnerIds.Add(user.Id);
                await _userScanResultRepository.UpsertAsync(user.Id, itemList.Count, totalFileCount, scanDate);
                ((IProgress<ScanProgress>)progress).Report(new ScanProgress
                {
                    ProcessedUsers = ++processed,
                    TotalUsers = total,
                    CurrentUserName = user.DisplayName,
                    FoundItemsCount = allItems.Count
                });
            }

            await _repository.UpsertAsync(allItems, scannedOwnerIds);
            await _sharedItemsVm.LoadAsync();
            ScanStatus = ScanStatus.Completed;
            ProgressMessage = $"スキャン完了: {allItems.Count} 件の共有アイテムを検出";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = ScanStatus.Cancelled;
            ProgressMessage = "スキャンがキャンセルされました";
            if (allItems.Count > 0)
            {
                await _repository.UpsertAsync(allItems, scannedOwnerIds);
                await _sharedItemsVm.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            ScanStatus = ScanStatus.Failed;
            ProgressMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            CanCancel = false;
            _sharedItemsVm.IsScanRunning = false;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    public async Task RefreshUsersAsync()
    {
        try
        {
            var users = await _graphService.GetAllUsersAsync(ExcludeGuests);
            var scanResults = (await _userScanResultRepository.GetAllAsync())
                .ToDictionary(r => r.UserId);

            foreach (var user in users)
            {
                if (scanResults.TryGetValue(user.Id, out var result))
                {
                    user.RiskFiles = result.RiskFiles;
                    user.AllFiles = result.AllFiles;
                    user.LastCheckDate = result.LastCheckDate;
                }
            }

            _allUsers.Clear();
            _allUsers.AddRange(users);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ScanLog += $"[ERROR] ユーザ情報の取得に失敗しました: {ex.Message}{Environment.NewLine}";
        }
    }

    [RelayCommand]
    private void Filter()
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = FilterKeyword.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _allUsers
            : _allUsers.Where(u =>
                u.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Users.Clear();
        foreach (var user in filtered)
            Users.Add(user);
    }
}
