using Microsoft.Win32;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.Infrastructure.Settings;

/// <summary>
/// Azure AD 接続設定を HKCU レジストリで永続化するサービス
/// </summary>
public class RegistrySettingsService : ISettingsService
{
    private const string RegistryKey = @"Software\OneDriveAccessGuard";

    public string? ClientId { get; set; }
    public string? TenantId { get; set; }
    public string? CertificateThumbprint { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(CertificateThumbprint);

    public RegistrySettingsService()
    {
        Load();
    }

    public void Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
        if (key == null) return;

        ClientId              = key.GetValue(nameof(ClientId))              as string;
        TenantId              = key.GetValue(nameof(TenantId))              as string;
        CertificateThumbprint = key.GetValue(nameof(CertificateThumbprint)) as string;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
        key.SetValue(nameof(ClientId),              ClientId              ?? string.Empty);
        key.SetValue(nameof(TenantId),              TenantId              ?? string.Empty);
        key.SetValue(nameof(CertificateThumbprint), CertificateThumbprint ?? string.Empty);
    }
}
