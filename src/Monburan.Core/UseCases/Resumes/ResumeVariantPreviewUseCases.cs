using System.Text.Json;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>Preview da variante: estrutura do currículo base + textos localizados dos overrides.</summary>
public sealed class GetResumeVariantPreviewUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IResumeRepository _resumes;
    private readonly IFeatureFlags _featureFlags;

    public GetResumeVariantPreviewUseCase(IResumeVariantRepository variants, IResumeRepository resumes, IFeatureFlags featureFlags)
    {
        _variants = variants;
        _resumes = resumes;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<ResumeVariantPreviewResponse>> ExecuteAsync(ResumeVariantByIdCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<ResumeVariantPreviewResponse>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var variant = await _variants.GetByIdAsync(Id.Create(command.VariantId), cancellationToken);
        if (variant.Status != EResultStatus.Success || variant.Data is null)
            return RequestResult<ResumeVariantPreviewResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");
        if (variant.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<ResumeVariantPreviewResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");

        var resume = await _resumes.GetByIdAsync(variant.Data.ResumeId, cancellationToken);
        if (resume.Status != EResultStatus.Success || resume.Data is null)
            return RequestResult<ResumeVariantPreviewResponse>.EntityNotFound("Resume", variant.Data.ResumeId.Value, "Currículo base não encontrado.");

        var overrides = ResumeVariantLocalization.ParseOverrides(variant.Data.OverridesJson);
        var baseResume = resume.Data;
        var experience = baseResume.ExperienceHistory.ToList();
        var education = baseResume.EducationHistory.ToList();
        var skills = baseResume.Skills.ToList();
        var languages = baseResume.Languages.ToList();

        return RequestResult<ResumeVariantPreviewResponse>.Success(new ResumeVariantPreviewResponse
        {
            VariantId = variant.Data.Id.Value,
            Locale = variant.Data.Locale,
            Title = ResumeVariantLocalization.Localize(overrides, "title", baseResume.Title),
            Summary = ResumeVariantLocalization.Localize(overrides, "summary", baseResume.Summary),
            CandidateName = baseResume.Candidate?.Name,
            Experience = experience.Select((e, i) => new VariantExperienceItem
            {
                Position = ResumeVariantLocalization.Localize(overrides, $"experience:{i}:position", e.Position),
                Company = ResumeVariantLocalization.Localize(overrides, $"experience:{i}:company", e.Company),
                Description = ResumeVariantLocalization.Localize(overrides, $"experience:{i}:description", e.Description),
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Education = education.Select((e, i) => new VariantEducationItem
            {
                Course = ResumeVariantLocalization.Localize(overrides, $"education:{i}:course", e.Course),
                Institution = ResumeVariantLocalization.Localize(overrides, $"education:{i}:institution", e.Institution),
                Level = e.Level.ToString(),
                StartDate = e.StartDate,
                EndDate = e.EndDate
            }).ToList(),
            Skills = skills.Select((s, i) => ResumeVariantLocalization.Localize(overrides, $"skills:{i}", s.Skill)).ToList(),
            Languages = languages.Select((l, i) => new VariantLanguageItem
            {
                Name = ResumeVariantLocalization.Localize(overrides, $"languages:{i}", l.Name),
                Level = l.Level.ToString()
            }).ToList(),
            PendingSuggestions = variant.Data.PendingSuggestionsJson is null
                ? null
                : ResumeVariantLocalization.ParseOverrides(variant.Data.PendingSuggestionsJson)
        });
    }
}

/// <summary>
/// Gera sugestões de localização com IA e guarda como pendentes — nada é aplicado até a
/// pessoa revisar e confirmar (aceitar/rejeitar explícito).
/// </summary>
public sealed class GenerateVariantSuggestionsUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IResumeRepository _resumes;
    private readonly IAiTextClient _ai;
    private readonly IFeatureFlags _featureFlags;
    private readonly IAiUsageLimiter _usageLimiter;

    public GenerateVariantSuggestionsUseCase(
        IResumeVariantRepository variants,
        IResumeRepository resumes,
        IAiTextClient ai,
        IFeatureFlags featureFlags,
        IAiUsageLimiter usageLimiter)
    {
        _variants = variants;
        _resumes = resumes;
        _ai = ai;
        _featureFlags = featureFlags;
        _usageLimiter = usageLimiter;
    }

    public async Task<RequestResult<ResumeVariantResponse>> ExecuteAsync(ResumeVariantByIdCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var variant = await _variants.GetByIdAsync(Id.Create(command.VariantId), cancellationToken);
        if (variant.Status != EResultStatus.Success || variant.Data is null)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");
        if (variant.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");

        if (!_ai.IsConfigured)
            return RequestResult<ResumeVariantResponse>.WithDomainError("A assistente de IA não está configurada neste ambiente.");

        if (!_usageLimiter.TryAcquire($"variant-ai:{command.CandidateId}", out var retryAfter))
            return RequestResult<ResumeVariantResponse>.WithDomainError(
                $"Limite de uso da IA atingido. Tente novamente em {Math.Ceiling(retryAfter.TotalMinutes)} minuto(s).");

        var resume = await _resumes.GetByIdAsync(variant.Data.ResumeId, cancellationToken);
        if (resume.Status != EResultStatus.Success || resume.Data is null)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("Resume", variant.Data.ResumeId.Value, "Currículo base não encontrado.");

        var prompt = BuildLocalizationPrompt(resume.Data, variant.Data.Locale);
        AiCompletion completion;
        try
        {
            completion = await _ai.CompleteAsync(new AiCompletionRequest(prompt, JsonObject: true), cancellationToken);
        }
        catch (AiClientException)
        {
            return RequestResult<ResumeVariantResponse>.WithDomainError(
                "A IA não respondeu agora. Nenhuma alteração foi feita — tente novamente em instantes.");
        }

        Dictionary<string, string> suggestions;
        try
        {
            suggestions = JsonSerializer.Deserialize<Dictionary<string, string>>(completion.Content)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return RequestResult<ResumeVariantResponse>.WithDomainError(
                "A IA respondeu num formato inesperado. Nenhuma alteração foi feita.");
        }

        // Só guardam chaves conhecidas — o que a IA inventar fora do contrato é descartado
        suggestions = suggestions
            .Where(kv => ResumeVariantLocalization.IsValidKey(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());

        if (suggestions.Count == 0)
            return RequestResult<ResumeVariantResponse>.WithDomainError(
                "A IA não produziu sugestões aproveitáveis. Nenhuma alteração foi feita.");

        var entity = variant.Data;
        entity.PendingSuggestionsJson = ResumeVariantLocalization.Serialize(suggestions);
        var updated = await _variants.UpdateAsync(entity, cancellationToken);
        if (updated.Status != EResultStatus.Success)
            return RequestResult<ResumeVariantResponse>.FromResult(updated);

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(updated.Data!));
    }

    private static IReadOnlyList<AiMessage> BuildLocalizationPrompt(Resume resume, string locale)
    {
        var education = resume.EducationHistory.ToList();
        var experience = resume.ExperienceHistory.ToList();
        var skills = resume.Skills.ToList();
        var languages = resume.Languages.ToList();

        var sections = new Dictionary<string, string?> { ["title"] = resume.Title, ["summary"] = resume.Summary };
        for (var i = 0; i < experience.Count; i++)
        {
            sections[$"experience:{i}:position"] = experience[i].Position;
            sections[$"experience:{i}:company"] = experience[i].Company;
            sections[$"experience:{i}:description"] = experience[i].Description;
        }
        for (var i = 0; i < education.Count; i++)
        {
            sections[$"education:{i}:course"] = education[i].Course;
            sections[$"education:{i}:institution"] = education[i].Institution;
        }
        for (var i = 0; i < skills.Count; i++)
            sections[$"skills:{i}"] = skills[i].Skill;
        for (var i = 0; i < languages.Count; i++)
            sections[$"languages:{i}"] = languages[i].Name;

        var payload = JsonSerializer.Serialize(sections, new JsonSerializerOptions { WriteIndented = false });

        return
        [
            new AiMessage("system",
                "Você localiza currículos. Recebe um JSON cujas chaves são campos do currículo e responde " +
                "APENAS com outro JSON de mesmas chaves, cujos valores são a versão localizada no idioma/país pedido. " +
                "Preserve nomes próprios (empresas, instituições). Traduza cargos, descrições e habilidades. " +
                "Não invente informação; quando não houver nada para localizar, mantenha o valor original. " +
                "Não explique, não comente: somente o JSON."),
            new AiMessage("user", $"Localize para {locale}:\n{payload}")
        ];
    }
}

/// <summary>Aceitar aplica as sugestões pendentes aos overrides; rejeitar descarta. Sempre explícito.</summary>
public sealed class ResolveVariantSuggestionsUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public ResolveVariantSuggestionsUseCase(IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _variants = variants;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<ResumeVariantResponse>> ExecuteAsync(ResolveVariantSuggestionsCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var variant = await _variants.GetByIdAsync(Id.Create(command.VariantId), cancellationToken);
        if (variant.Status != EResultStatus.Success || variant.Data is null)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");
        if (variant.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");
        if (variant.Data.PendingSuggestionsJson is null)
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "Não há sugestões pendentes para revisar.", "Suggestions");

        var entity = variant.Data;
        if (command.Accept)
        {
            var overrides = ResumeVariantLocalization.ParseOverrides(entity.OverridesJson);
            foreach (var (key, value) in ResumeVariantLocalization.ParseOverrides(entity.PendingSuggestionsJson))
                overrides[key] = value;
            entity.OverridesJson = ResumeVariantLocalization.Serialize(overrides);
        }
        entity.PendingSuggestionsJson = null;

        var updated = await _variants.UpdateAsync(entity, cancellationToken);
        if (updated.Status != EResultStatus.Success)
            return RequestResult<ResumeVariantResponse>.FromResult(updated);

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(updated.Data!));
    }
}
