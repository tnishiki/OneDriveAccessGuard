using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty] private bool _isSigningIn;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public event EventHandler? SignInSucceeded;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsSigningIn = true;
        ErrorMessage = string.Empty;

        try
        {
            var success = await _authService.SignInAsync();
            if (success)
                SignInSucceeded?.Invoke(this, EventArgs.Empty);
            else
                ErrorMessage = "サインインに失敗しました。再度お試しください。";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            IsSigningIn = false;
        }
    }
}
