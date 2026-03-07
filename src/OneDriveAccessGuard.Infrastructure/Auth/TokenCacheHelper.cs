using Microsoft.Identity.Client;
using System.Security.Cryptography;

namespace OneDriveAccessGuard.Infrastructure.Auth;

/// <summary>
/// MSALトークンキャッシュをWindowsのDPAPIで暗号化してローカル保存するヘルパー
/// </summary>
internal static class TokenCacheHelper
{
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OneDriveAccessGuard",
        "token_cache.bin");

    private static readonly object FileLock = new();

    public static void EnableSerialization(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (FileLock)
        {
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    var encryptedData = File.ReadAllBytes(CacheFilePath);
                    // DPAPIで復号化（現在のWindowsユーザーのみ復号可能）
                    var decryptedData = ProtectedData.Unprotect(
                        encryptedData,
                        null,
                        DataProtectionScope.CurrentUser);
                    args.TokenCache.DeserializeMsalV3(decryptedData);
                }
                catch
                {
                    // キャッシュが破損している場合は削除して再認証
                    File.Delete(CacheFilePath);
                }
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                var data = args.TokenCache.SerializeMsalV3();
                // DPAPIで暗号化
                var encryptedData = ProtectedData.Protect(
                    data,
                    null,
                    DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CacheFilePath, encryptedData);
            }
        }
    }
}
