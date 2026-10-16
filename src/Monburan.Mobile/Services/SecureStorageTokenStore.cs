using Monburan.Mobile.Shared.Auth;

namespace Monburan.Mobile.Services;

/// <summary>
/// Persiste a sessão no SecureStorage (KeyStore no Android / Keychain no iOS),
/// sobrevivendo a reinício do app.
/// </summary>
public class SecureStorageTokenStore : ITokenStore
{
    private const string AccessTokenKey = "monburan.access_token";
    private const string RefreshTokenKey = "monburan.refresh_token";

    public async Task<AuthSession?> LoadAsync()
    {
        try
        {
            var access = await SecureStorage.Default.GetAsync(AccessTokenKey);
            var refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey);

            return string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)
                ? null
                : new AuthSession(access, refresh);
        }
        catch (Exception)
        {
            // SecureStorage pode falhar em devices sem lockscreen — trata como deslogado
            return null;
        }
    }

    public async Task SaveAsync(AuthSession session)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, session.AccessToken);
            await SecureStorage.Default.SetAsync(RefreshTokenKey, session.RefreshToken);
        }
        catch (Exception)
        {
            // Devices sem lockscreen/Keystore: a sessão vive só em memória neste boot
        }
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
        }
        catch (Exception)
        {
            // Idem SaveAsync — nada a limpar se o storage não está disponível
        }
        return Task.CompletedTask;
    }
}
