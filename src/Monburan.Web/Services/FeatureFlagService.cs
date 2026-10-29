using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

/// <summary>
/// Avalia as flags para a conta. As flags ficam em cache por 30 segundos e os grupos atribuídos de cada conta por 1
/// minuto: a participação é lida do banco (claims do Identity), então incluir alguém num grupo vale sem novo login, no site
/// e no app. Mudanças pela tela de administração descartam o cache na hora.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlags
{
    private static readonly TimeSpan FlagsTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GroupsTtl = TimeSpan.FromMinutes(1);
    private const string FlagsKey = "feature-flags:all";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureFlagService> _logger;
    // Trocado a cada Invalidate() geral: as chaves antigas de grupos por conta deixam de ser lidas
    private long _generation;

    public FeatureFlagService(IDbContextFactory<ApplicationDbContext> contextFactory, IMemoryCache cache, ILogger<FeatureFlagService> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string feature, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        var flags = await FlagsAsync(cancellationToken);
        if (!flags.TryGetValue(feature, out var flag))
            return false;

        if (flag.EnabledForEveryone)
            return true;

        return FeatureFlagRules.IsEnabled(flag, await GroupsOfAsync(user, cancellationToken));
    }

    public async Task<IReadOnlyList<string>> EnabledFeaturesAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        var flags = await FlagsAsync(cancellationToken);
        var groups = await GroupsOfAsync(user, cancellationToken);
        return flags.Values.Where(flag => FeatureFlagRules.IsEnabled(flag, groups)).Select(flag => flag.Name).Order().ToList();
    }

    public void Invalidate(Guid? userId = null)
    {
        if (userId is { } id)
        {
            _cache.Remove(UserKey(id));
            return;
        }

        _cache.Remove(FlagsKey);
        Interlocked.Increment(ref _generation);
    }

    public static Guid? UserIdOf(ClaimsPrincipal? user)
    {
        var value = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private async Task<IReadOnlyDictionary<string, FeatureFlag>> FlagsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(FlagsKey, out IReadOnlyDictionary<string, FeatureFlag>? cached) && cached is not null)
            return cached;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var flags = await context.FeatureFlags.AsNoTracking().Where(f => !f.IsDeleted).ToListAsync(cancellationToken);
            var map = flags.ToDictionary(f => f.Name, StringComparer.Ordinal);
            _cache.Set(FlagsKey, (IReadOnlyDictionary<string, FeatureFlag>)map, FlagsTtl);
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Banco fora do ar: tudo que está atrás de flag fica desligado, o resto do site continua
            _logger.LogWarning(ex, "Feature flags: não foi possível ler as flags; todas ficam desligadas até a próxima leitura");
            return new Dictionary<string, FeatureFlag>();
        }
    }

    private async Task<IReadOnlySet<string>> GroupsOfAsync(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var groups = new HashSet<string>(AccessGroups.ImplicitOf(user), StringComparer.Ordinal);
        if (UserIdOf(user) is not { } userId)
            return groups;

        var key = UserKey(userId);
        if (!_cache.TryGetValue(key, out string[]? assigned) || assigned is null)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                assigned = await context.UserClaims.AsNoTracking()
                    .Where(c => c.UserId == userId && c.ClaimType == AccessGroups.ClaimType && c.ClaimValue != null)
                    .Select(c => c.ClaimValue!)
                    .ToArrayAsync(cancellationToken);
                _cache.Set(key, assigned, GroupsTtl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Feature flags: não foi possível ler os grupos da conta; valem só os implícitos");
                assigned = [];
            }
        }

        groups.UnionWith(assigned);
        return groups;
    }

    private string UserKey(Guid userId) => $"feature-flags:user:{Interlocked.Read(ref _generation)}:{userId}";
}
