using System.Security.Claims;
using System.Text.RegularExpressions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed record ReplaceCandidateLanguagesCommand(Guid CandidateId, CandidateLanguagesRequest Languages);

/// <summary>Substitui o conjunto de idiomas autodeclarados do candidato (sem veredito da plataforma).</summary>
public sealed class ReplaceCandidateLanguagesUseCase
{
    private const int MaxLanguages = 15;
    // BCP-47 simples: idioma minúsculo + região maiúscula opcional (ex.: "es", "es-ES", "en-US", "pt-BR")
    private static readonly Regex TagFormat = new("^[a-z]{2,3}(-[A-Z]{2})?$", RegexOptions.Compiled);

    private readonly ICandidateLanguageRepository _languages;
    private readonly IFeatureFlags _featureFlags;

    public ReplaceCandidateLanguagesUseCase(ICandidateLanguageRepository languages, IFeatureFlags featureFlags)
    {
        _languages = languages;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<CandidateLanguagesResponse>> ExecuteAsync(
        ReplaceCandidateLanguagesCommand command, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(InternationalCareerFeature.Name, user, cancellationToken))
            return RequestResult<CandidateLanguagesResponse>.WithValidationError(
                "As preferências internacionais não estão habilitadas para esta conta.", "Feature");

        var errors = new List<RequestValidation>();
        var items = command.Languages.Languages ?? [];

        if (items.Count > MaxLanguages)
            errors.Add(new(nameof(command.Languages.Languages), $"Informe no máximo {MaxLanguages} idiomas."));

        var normalized = new List<CandidateLanguageItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var tag = NormalizeTag(item.LanguageTag);
            if (tag is null || !TagFormat.IsMatch(tag) || !seen.Add(tag))
            {
                errors.Add(new(nameof(command.Languages.Languages),
                    $"Idioma inválido ou repetido: “{item.LanguageTag}”. Use o formato BCP-47 (ex.: es-ES, en-US)."));
                continue;
            }
            if (!Enum.IsDefined(item.Level) || !Enum.IsDefined(item.Evidence))
            {
                errors.Add(new(nameof(command.Languages.Languages), $"Nível ou evidência inválidos para “{item.LanguageTag}”."));
                continue;
            }
            normalized.Add(new CandidateLanguageItem { LanguageTag = tag, Level = item.Level, Evidence = item.Evidence });
        }
        if (errors.Count != 0)
            return RequestResult<CandidateLanguagesResponse>.WithValidations(errors);

        var candidateId = Id.Create(command.CandidateId);
        var entities = normalized.Select(item => new CandidateLanguage
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = candidateId,
            LanguageTag = item.LanguageTag,
            Level = item.Level,
            Evidence = item.Evidence,
            TenantId = "default"
        }).ToList();

        var result = await _languages.ReplaceForCandidateAsync(candidateId, entities, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<CandidateLanguagesResponse>.FromResult(result);

        return RequestResult<CandidateLanguagesResponse>.Success(new CandidateLanguagesResponse
        {
            Languages = result.Data!.Select(item => new CandidateLanguageItem
            {
                LanguageTag = item.LanguageTag,
                Level = item.Level,
                Evidence = item.Evidence
            }).ToList()
        });
    }

    // "es-es" -> "es-ES"; "EN-us" -> "en-US"; "pt" -> "pt"
    private static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var pieces = tag.Trim().Split('-', 2, StringSplitOptions.TrimEntries);
        var language = pieces[0].ToLowerInvariant();
        return pieces.Length == 2 ? $"{language}-{pieces[1].ToUpperInvariant()}" : language;
    }
}
