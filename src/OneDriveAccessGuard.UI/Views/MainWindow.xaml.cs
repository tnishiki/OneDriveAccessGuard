using OneDriveAccessGuard.UI.ViewModels;
using System.Windows;

namespace OneDriveAccessGuard.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
