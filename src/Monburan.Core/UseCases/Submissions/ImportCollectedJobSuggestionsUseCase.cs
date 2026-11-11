using System.Security.Claims;

using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Submissions;

/// <summary>Importa itens capturados de fontes públicas para a fila de moderação; nunca publica vagas diretamente.</summary>
public sealed class ImportCollectedJobSuggestionsUseCase
{
    private const int MaxBatch = 250;
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "empregos.itajai.sc.gov.br",
        "itajaionline.com.br",
        "www.itajaionline.com.br",
        "joinvilleempregamais.empregaja.org",
        "emprega.curitiba.pr.gov.br",
        "www.pgempregos.com.br",
        "pgempregos.com.br"
    };

    private readonly IJobSubmissionRepository _submissions;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<ImportCollectedJobSuggestionsUseCase> _logger;

    public ImportCollectedJobSuggestionsUseCase(
        IJobSubmissionRepository submissions,
        IFeatureFlags featureFlags,
        ILogger<ImportCollectedJobSuggestionsUseCase> logger)
    {
        _submissions = submissions;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public async Task<RequestResult<ImportCollectedJobSuggestionsResponse>> ExecuteAsync(
        ImportCollectedJobSuggestionsRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync("captacao-vagas", user, cancellationToken))
            return RequestResult<ImportCollectedJobSuggestionsResponse>.WithValidationError(
                "A captação de vagas não está habilitada para esta conta.", "Feature");

        if (request.Jobs.Count == 0)
            return RequestResult<ImportCollectedJobSuggestionsResponse>.WithValidationError("O arquivo não contém vagas.", "Jobs");
        if (request.Jobs.Count > MaxBatch)
            return RequestResult<ImportCollectedJobSuggestionsResponse>.WithValidationError(
                $"Importe no máximo {MaxBatch} vagas por arquivo.", "Jobs");

        foreach (var input in request.Jobs)
        {
            var invalid = input is null ? "O arquivo contém um item vazio." : Validate(input);
            if (invalid is not null)
                return RequestResult<ImportCollectedJobSuggestionsResponse>.WithValidationError(invalid, "Jobs");
        }

        var imported = 0;
        var duplicates = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in request.Jobs)
        {
            var sourceUrl = CanonicalSourceUrl(input.SourceUrl);
            if (!seen.Add(sourceUrl))
            {
                duplicates++;
                continue;
            }

            var matches = await _submissions.FindDuplicatesAsync(
                input.Title,
                input.CompanyName,
                input.City,
                sourceUrl,
                [],
                null,
                cancellationToken);
            if (matches.Status == EResultStatus.Success && matches.Data?.Any(match => match.Reason.Contains("mesma fonte", StringComparison.OrdinalIgnoreCase)) == true)
            {
                duplicates++;
                continue;
            }

            var id = Id.Create(Guid.NewGuid());
            var submission = new JobSubmission
            {
                Id = id,
                Title = input.Title.Trim(),
                CompanyName = Clean(input.CompanyName),
                City = Clean(input.City),
                State = Clean(input.State),
                Description = Limit(Clean(input.Description), 2000),
                SourceUrl = sourceUrl,
                SourceDescription = Limit($"{input.SourceName.Trim()} · capturada em {input.CapturedAt:dd/MM/yyyy HH:mm} · região {input.Region.Trim()}", 500),
                Status = ESubmissionStatus.Pending,
                TenantId = "default"
            };
            submission.History.Add(new JobSubmissionHistory
            {
                Id = Id.Create(Guid.NewGuid()),
                SubmissionId = id,
                Status = ESubmissionStatus.Pending,
                Note = "Importada pelo coletor de fontes públicas; aguarda revisão humana.",
                TenantId = "default"
            });

            var created = await _submissions.CreateAsync(submission, cancellationToken);
            if (created.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Não foi possível importar vaga coletada da fonte {SourceName}", input.SourceName);
                return RequestResult<ImportCollectedJobSuggestionsResponse>.FromResult(created);
            }

            imported++;
        }

        return RequestResult<ImportCollectedJobSuggestionsResponse>.Success(
            new ImportCollectedJobSuggestionsResponse(imported, duplicates));
    }

    private static string? Validate(CollectedJobInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200)
            return "Há uma vaga sem cargo válido (máximo de 200 caracteres).";
        if (string.IsNullOrWhiteSpace(input.SourceName) || input.SourceName.Trim().Length > 200)
            return "Há uma vaga sem nome de fonte válido.";
        if (string.IsNullOrWhiteSpace(input.Region) || input.Region.Trim().Length > 100)
            return "Há uma vaga sem região válida.";
        if (input.CapturedAt == default)
            return "Há uma vaga sem horário de coleta válido.";
        if (input.Description?.Length > 2000 || input.CompanyName?.Length > 200 || input.City?.Length > 200 || input.State?.Length > 200)
            return "Uma das vagas excede o tamanho permitido em seus dados.";
        if (!Uri.TryCreate(input.SourceUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !AllowedHosts.Contains(uri.Host))
            return "O link de origem não pertence a um dos portais públicos configurados.";
        return null;
    }

    private static string CanonicalSourceUrl(string sourceUrl)
    {
        var uri = new Uri(sourceUrl.Trim());
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Limit(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
