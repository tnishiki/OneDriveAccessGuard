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
    public ObservableCollection<SharedItem> ScannedItems { get; } = new();

    public ScanViewModel(IGraphService graphService, ISharedItemRepository repository, SharedItemsViewModel sharedItemsVm)
    {
        _graphService = graphService;
        _repository = repository;
        _sharedItemsVm = sharedItemsVm;
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
        var uiContext = SynchronizationContext.Current!;
        
        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressPercent = p.ProgressPercent;
            ProgressMessage = $"スキャン中: {p.CurrentUserName} ({p.ProcessedUsers}/{p.TotalUsers})";
            FoundItemsCount = p.FoundItemsCount;
        });

        var allItems = new List<SharedItem>();
        try
        {
            var users = await _graphService.GetAllUsersAsync(ExcludeGuests, _cts.Token);
            int processed = 0;
            int total = users.Count();
            var allItemsBag = new System.Collections.Concurrent.ConcurrentBag<SharedItem>();
            var semaphore = new SemaphoreSlim(5); // 同時5ユーザーまで

            var userTasks = users.Select(async user =>
            {
                await semaphore.WaitAsync(_cts.Token);
                try
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var items = await _graphService.GetSharedItemsAsync(
                        user.Id,user.DisplayName , progress, _cts.Token);
                    foreach (var item in items)
                    {
                        item.OwnerDisplayName = user.DisplayName;
                        item.OwnerEmail = user.Email;
                        allItemsBag.Add(item);
                        uiContext.Post(_ => ScannedItems.Add(item), null);
                    }
                    var count = Interlocked.Increment(ref processed);
                    ((IProgress<ScanProgress>)progress).Report(new ScanProgress
                    {
                        ProcessedUsers = count,
                        TotalUsers = total,
                        CurrentUserName = user.DisplayName,
                        FoundItemsCount = allItemsBag.Count
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(userTasks);

            // ConcurrentBag → List に変換
            allItems.AddRange(allItemsBag);

            await _repository.UpsertAsync(allItems);
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
                await _repository.UpsertAsync(allItems);
                await _sharedItemsVm.LoadAsync();
            }
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            ScanStatus = ScanStatus.Cancelled;
            ProgressMessage = "スキャンがキャンセルされました";
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
}
