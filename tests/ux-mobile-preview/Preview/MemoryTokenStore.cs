using Monburan.Mobile.Shared.Auth;

namespace Monburan.Mobile.Preview;

/// <summary>Sessão só em memória: substitui o SecureStorage do aparelho. Um processo = uma pessoa logada.</summary>
public sealed class MemoryTokenStore : ITokenStore
{
    private AuthSession? _session;

    public Task<AuthSession?> LoadAsync() => Task.FromResult(_session);

    public Task SaveAsync(AuthSession session)
    {
        _session = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _session = null;
        return Task.CompletedTask;
    }
}
