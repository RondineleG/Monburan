using Microsoft.Extensions.Logging;

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

    private readonly ILogger<SecureStorageTokenStore> _logger;

    public SecureStorageTokenStore(ILogger<SecureStorageTokenStore> logger)
    {
        _logger = logger;
    }

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
        catch (Exception ex)
        {
            // SecureStorage pode falhar em devices sem lockscreen — trata como deslogado
            _logger.LogWarning(ex, "SecureStorage indisponível ao carregar a sessão");
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
        catch (Exception ex)
        {
            // Devices sem lockscreen/Keystore: a sessão vive só em memória neste boot
            _logger.LogWarning(ex, "SecureStorage indisponível ao salvar a sessão");
        }
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
        }
        catch (Exception ex)
        {
            // Idem SaveAsync — nada a limpar se o storage não está disponível
            _logger.LogWarning(ex, "SecureStorage indisponível ao limpar a sessão");
        }
        return Task.CompletedTask;
    }
}
