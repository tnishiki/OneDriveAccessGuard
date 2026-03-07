using OneDriveAccessGuard.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OneDriveAccessGuard.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
