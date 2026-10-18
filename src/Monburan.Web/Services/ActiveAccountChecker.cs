using Microsoft.Extensions.Caching.Memory;

namespace Monburan.Web.Services;

/// <summary>
/// Confere se a conta de um token de acesso (JWT) continua ativa. O token vale até expirar (1 hora), então sem isto uma
/// conta inativada seguiria usando a API. O resultado fica em memória por 1 minuto, para não consultar o banco a cada
/// requisição; a inativação limpa a entrada na hora (<see cref="Invalidate"/>).
/// </summary>
public sealed class ActiveAccountChecker(IMemoryCache cache, IServiceScopeFactory scopes)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public async Task<bool> IsActiveAsync(string userId)
    {
        if (cache.TryGetValue(Key(userId), out bool known))
            return known;

        await using var scope = scopes.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId);
        var active = user is { IsActive: true };

        cache.Set(Key(userId), active, Lifetime);
        return active;
    }

    public void Invalidate(string userId) => cache.Remove(Key(userId));

    private static string Key(string userId) => "conta-ativa:" + userId;
}
