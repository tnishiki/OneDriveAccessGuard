using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IGraphService _graphService;
    private readonly ISharedItemRepository _repository;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ScanStatus _scanStatus = ScanStatus.Idle;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressMessage = "スキャン待機中";
    [ObservableProperty] private int _foundItemsCount;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _excludeGuests = true;

    public ObservableCollection<SharedItem> ScannedItems { get; } = new();

    public ScanViewModel(IGraphService graphService, ISharedItemRepository repository)
    {
        _graphService = graphService;
        _repository = repository;
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        if (ScanStatus == ScanStatus.Running) return;

        _cts = new CancellationTokenSource();
        ScanStatus = ScanStatus.Running;
        CanCancel = true;
        FoundItemsCount = 0;
        ScannedItems.Clear();

        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressPercent = p.ProgressPercent;
            ProgressMessage = $"スキャン中: {p.CurrentUserName} ({p.ProcessedUsers}/{p.TotalUsers})";
            FoundItemsCount = p.FoundItemsCount;
        });

        try
        {
            var users = await _graphService.GetAllUsersAsync(ExcludeGuests, _cts.Token);
            var allItems = new List<SharedItem>();

            int processed = 0;
            int total = users.Count();

            foreach (var user in users)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var items = await _graphService.GetSharedItemsAsync(
                    user.Id, progress, _cts.Token);

                foreach (var item in items)
                {
                    item.OwnerDisplayName = user.DisplayName;
                    item.OwnerEmail = user.Email;
                    ScannedItems.Add(item);
                }

                allItems.AddRange(items);
                processed++;

                ((IProgress<ScanProgress>)progress).Report(new ScanProgress
                {
                    ProcessedUsers = processed,
                    TotalUsers = total,
                    CurrentUserName = user.DisplayName,
                    FoundItemsCount = allItems.Count
                });
            }

            await _repository.UpsertAsync(allItems);
            ScanStatus = ScanStatus.Completed;
            ProgressMessage = $"スキャン完了: {allItems.Count} 件の共有アイテムを検出";
        }
        catch (OperationCanceledException)
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
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
    }
}
