using OneDriveAccessGuard.UI.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace OneDriveAccessGuard.UI.Views;

public partial class SharedItemsView : UserControl
{
    public SharedItemsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SharedItemsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
            Clipboard.SetText(url);
    }
}
