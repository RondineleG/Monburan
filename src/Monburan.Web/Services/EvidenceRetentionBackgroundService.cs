using Microsoft.EntityFrameworkCore;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Storage;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

/// <summary>
/// Apaga, uma vez por dia, as imagens de sugestões rejeitadas ou marcadas como duplicadas há mais de
/// <c>Evidence:RetentionDays</c> dias (padrão 90). O registro da evidência continua (nome, tamanho e hash, que servem para
/// achar duplicadas); só o arquivo some. Sugestões pendentes e aprovadas não são tocadas.
/// </summary>
/// <remarks>
/// A passagem é incremental: guarda em memória até onde já conferiu (<c>_purgedUpTo</c>, o limite de data da última
/// passada que terminou sem erro) e a seguinte só consulta o intervalo novo. Sem isso, a consulta e os N
/// <c>File.Exists</c>/<c>Delete</c> cresciam para sempre, todo dia, repetindo as mesmas sugestões antigas. A marca fica
/// em memória de propósito, para não criar uma migration só para isso: após um reinício a primeira passada confere tudo
/// uma vez (apagar um arquivo que já não existe não faz nada) e volta a ser incremental. <c>ModeratedAt</c> é regravado
/// em cada decisão, então uma sugestão que muda de situação entra de novo no intervalo seguinte.
/// </remarks>
public sealed class EvidenceRetentionBackgroundService : BackgroundService
{
    private const int DefaultRetentionDays = 90;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private static readonly ESubmissionStatus[] Closed = [ESubmissionStatus.Rejected, ESubmissionStatus.Duplicate];

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly ILogger<EvidenceRetentionBackgroundService> _logger;

    // Tudo com ModeratedAt abaixo deste instante já foi conferido por uma passada que terminou sem erro
    private DateTime? _purgedUpTo;

    public EvidenceRetentionBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        TimeProvider time,
        ILogger<EvidenceRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera o boot (migrations) terminar antes da primeira passada
        await Task.Delay(TimeSpan.FromMinutes(5), _time, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao aplicar a retenção das evidências de sugestões");
            }

            await Task.Delay(CheckInterval, _time, stoppingToken);
        }
    }

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var days = _configuration.GetValue("Evidence:RetentionDays", DefaultRetentionDays);
        if (days <= 0)
            return 0; // retenção desligada

        var cutoff = _time.GetUtcNow().UtcDateTime.AddDays(-days);

        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var storage = scope.ServiceProvider.GetRequiredService<IEvidenceStorage>();

        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var query = context.JobSubmissions.AsNoTracking()
            .Where(s => Closed.Contains(s.Status) && s.ModeratedAt != null && s.ModeratedAt < cutoff);
        if (_purgedUpTo is { } from)
            query = query.Where(s => s.ModeratedAt >= from);

        var keys = await query
            .SelectMany(s => s.Evidences.Select(e => e.StorageKey))
            .ToListAsync(cancellationToken);

        // Apagar um arquivo que já não existe não faz nada, então repetir uma passada interrompida é seguro
        foreach (var key in keys)
            await storage.DeleteAsync(key, cancellationToken);

        // A marca só avança depois de a passada inteira terminar: uma falha no meio faz a próxima repetir o intervalo.
        // Nunca recua (se a retenção for aumentada, o limite novo fica antes da marca e nada novo entra)
        if (_purgedUpTo is null || cutoff > _purgedUpTo)
            _purgedUpTo = cutoff;

        _logger.LogDebug("Retenção: {Quantidade} imagem(ns) de sugestões encerradas há mais de {Dias} dias conferida(s)", keys.Count, days);
        return keys.Count;
    }
}
