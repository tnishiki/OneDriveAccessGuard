using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IGraphService _graphService;

    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private string _tenantId = string.Empty;
    [ObservableProperty] private string _certificateThumbprint = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSuccess;

    public SettingsViewModel(ISettingsService settings, IGraphService graphService)
    {
        _settings = settings;
        _graphService = graphService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        ClientId              = _settings.ClientId              ?? string.Empty;
        TenantId              = _settings.TenantId              ?? string.Empty;
        CertificateThumbprint = _settings.CertificateThumbprint ?? string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ClientId) ||
            string.IsNullOrWhiteSpace(TenantId) ||
            string.IsNullOrWhiteSpace(CertificateThumbprint))
        {
            StatusMessage = "すべての項目を入力してください。";
            IsSuccess = false;
            return;
        }

        _settings.ClientId              = ClientId.Trim();
        _settings.TenantId              = TenantId.Trim();
        _settings.CertificateThumbprint = CertificateThumbprint.Trim();
        _settings.Save();

        _graphService.ReinitializeClient();

        StatusMessage = "設定をレジストリに保存しました。";
        IsSuccess = true;
    }
}
