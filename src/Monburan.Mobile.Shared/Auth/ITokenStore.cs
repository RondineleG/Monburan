namespace Monburan.Mobile.Shared.Auth;

/// <summary>
/// Persistência da sessão autenticada. A implementação no app usa SecureStorage.
/// </summary>
public interface ITokenStore
{
    Task<AuthSession?> LoadAsync();

    Task SaveAsync(AuthSession session);

    Task ClearAsync();
}
