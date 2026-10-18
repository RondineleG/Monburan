using Microsoft.Extensions.Options;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Foto em memória dos provedores de IA. Troca a foto inteira de uma vez (quem lê nunca vê um estado pela metade) e,
/// se o banco falhar ao reler, mantém a anterior em vez de desligar a IA.
/// </summary>
public sealed class AiProviderCatalog : IAiProviderCatalog
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AiProviderCatalog> _logger;
    private volatile Snapshot _snapshot = Snapshot.Empty;

    public AiProviderCatalog(IServiceScopeFactory scopes, ILogger<AiProviderCatalog> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public AiProviderSettings? Default => _snapshot.Default;

    public IReadOnlyList<AiProviderSettings> All => _snapshot.All;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAiProviderRepository>();
            var result = await repository.ListAsync().ConfigureAwait(false);

            if (result.Status != EResultStatus.Success || result.Data is null)
            {
                _logger.LogWarning("IA: não foi possível reler os provedores ({Status}); a lista anterior continua valendo", result.Status);
                return;
            }

            var all = result.Data.Select(AiProviderSettings.From).ToList();
            _snapshot = new Snapshot(all, all.FirstOrDefault(provider => provider.IsDefault && provider.IsEnabled));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "IA: erro ao reler os provedores; a lista anterior continua valendo");
        }
    }

    private sealed record Snapshot(IReadOnlyList<AiProviderSettings> All, AiProviderSettings? Default)
    {
        public static Snapshot Empty { get; } = new([], null);
    }
}

/// <summary>Relê os provedores de tempos em tempos: cobre alterações feitas por outra instância da aplicação.</summary>
public sealed class AiProviderCatalogRefresher : BackgroundService
{
    private readonly IAiProviderCatalog _catalog;
    private readonly IOptionsMonitor<AiOptions> _options;

    public AiProviderCatalogRefresher(IAiProviderCatalog catalog, IOptionsMonitor<AiOptions> options)
    {
        _catalog = catalog;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var seconds = Math.Clamp(_options.CurrentValue.CatalogRefreshSeconds, 10, 3600);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await _catalog.ReloadAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
