using OneDriveAccessGuard.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

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
}
