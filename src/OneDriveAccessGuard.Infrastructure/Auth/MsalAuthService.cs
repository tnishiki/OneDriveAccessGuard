using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using OneDriveAccessGuard.Core.Interfaces;

namespace OneDriveAccessGuard.Infrastructure.Auth;

/// <summary>
/// MSAL を使用した Microsoft Entra ID 認証サービス
/// </summary>
public class MsalAuthService : IAuthService
{
    private readonly IPublicClientApplication _msalApp;
    private readonly ILogger<MsalAuthService> _logger;
    private AuthenticationResult? _lastResult;

    // Graph API に必要な権限スコープ
    private static readonly string[] Scopes =
    [
        "https://graph.microsoft.com/Files.Read.All",
        "https://graph.microsoft.com/Files.ReadWrite.All",
        "https://graph.microsoft.com/User.Read.All",
        "https://graph.microsoft.com/Sites.Read.All"
    ];

    public bool IsSignedIn => _lastResult != null && _lastResult.ExpiresOn > DateTimeOffset.UtcNow;
    public string? SignedInUserName => _lastResult?.Account?.Username;
    public string? SignedInUserEmail => _lastResult?.Account?.Username;

    public MsalAuthService(string clientId, string tenantId, ILogger<MsalAuthService> logger)
    {
        _logger = logger;
        _msalApp = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithDefaultRedirectUri()
            .Build();

        // トークンキャッシュをWindowsの資格情報マネージャーに保存
        TokenCacheHelper.EnableSerialization(_msalApp.UserTokenCache);
    }

    /// <inheritdoc/>
    public async Task<bool> SignInAsync(CancellationToken ct = default)
    {
        try
        {
            // まずサイレント認証を試行
            var accounts = await _msalApp.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account != null)
            {
                _lastResult = await _msalApp
                    .AcquireTokenSilent(Scopes, account)
                    .ExecuteAsync(ct);
                _logger.LogInformation("サイレント認証成功: {User}", SignedInUserName);
                return true;
            }
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogInformation("インタラクティブ認証が必要です");
        }

        try
        {
            // インタラクティブ認証にフォールバック
            _lastResult = await _msalApp
                .AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(ct);
            _logger.LogInformation("インタラクティブ認証成功: {User}", SignedInUserName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "認証に失敗しました");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task SignOutAsync()
    {
        var accounts = await _msalApp.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _msalApp.RemoveAsync(account);
        }
        _lastResult = null;
        _logger.LogInformation("サインアウト完了");
    }

    /// <inheritdoc/>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!IsSignedIn)
        {
            await SignInAsync(ct);
        }

        // トークン期限切れ間近なら自動更新
        var accounts = await _msalApp.GetAccountsAsync();
        _lastResult = await _msalApp
            .AcquireTokenSilent(Scopes, accounts.First())
            .ExecuteAsync(ct);

        return _lastResult.AccessToken;
    }
}
