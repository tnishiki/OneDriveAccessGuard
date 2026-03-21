using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using OneDriveAccessGuard.UI.ViewModels;

namespace OneDriveAccessGuard.UI.Views;

public partial class ScanView : UserControl
{
    public ScanView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ScanViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is ScanViewModel newVm)
                newVm.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScanViewModel.ScanLog)) return;
        if (sender is ScanViewModel vm && vm.ShowLatestLog)
            LogTextBox.ScrollToEnd();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
