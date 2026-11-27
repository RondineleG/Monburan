using System.Security.Claims;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

public sealed record ResumeVariantCommand(Guid CandidateId, Guid ResumeId, ClaimsPrincipal? User);
public sealed record CreateResumeVariantCommand(Guid CandidateId, Guid ResumeId, ResumeVariantCreateRequest Request, ClaimsPrincipal? User);
public sealed record ResumeVariantByIdCommand(Guid CandidateId, Guid VariantId, ClaimsPrincipal? User);
public sealed record UpdateResumeVariantCommand(Guid CandidateId, Guid VariantId, ResumeVariantUpdateRequest Request, ClaimsPrincipal? User);
public sealed record ResolveVariantSuggestionsCommand(Guid CandidateId, Guid VariantId, bool Accept, ClaimsPrincipal? User);

internal static class ResumeVariantMapping
{
    public static ResumeVariantResponse ToResponse(ResumeVariant variant) => new()
    {
        Id = variant.Id.Value,
        ResumeId = variant.ResumeId.Value,
        Locale = variant.Locale,
        Overrides = ResumeVariantLocalization.ParseOverrides(variant.OverridesJson),
        HasPendingSuggestions = variant.PendingSuggestionsJson is not null,
        IsActive = variant.IsActive,
        CreatedAt = variant.CreatedAt
    };
}

/// <summary>Lista as variantes localizadas de um currículo do candidato.</summary>
public sealed class ListResumeVariantsUseCase
{
    private readonly IResumeRepository _resumes;
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public ListResumeVariantsUseCase(IResumeRepository resumes, IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _resumes = resumes;
        _variants = variants;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<IEnumerable<ResumeVariantResponse>>> ExecuteAsync(ResumeVariantCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<IEnumerable<ResumeVariantResponse>>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var resume = await _resumes.GetByIdAsync(Id.Create(command.ResumeId), cancellationToken);
        if (resume.Status != EResultStatus.Success || resume.Data is null)
            return RequestResult<IEnumerable<ResumeVariantResponse>>.EntityNotFound("Resume", command.ResumeId, "Currículo não encontrado.");
        if (resume.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<IEnumerable<ResumeVariantResponse>>.EntityNotFound("Resume", command.ResumeId, "Currículo não encontrado.");

        var variants = await _variants.ListByResumeAsync(Id.Create(command.ResumeId), cancellationToken);
        if (variants.Status != EResultStatus.Success)
            return RequestResult<IEnumerable<ResumeVariantResponse>>.FromResult(variants);

        return RequestResult<IEnumerable<ResumeVariantResponse>>.Success(
            variants.Data!.Select(ResumeVariantMapping.ToResponse).ToList());
    }
}

/// <summary>Cria a variante localizada (ex.: es-ES) sem tocar o currículo base.</summary>
public sealed class CreateResumeVariantUseCase
{
    private readonly IResumeRepository _resumes;
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public CreateResumeVariantUseCase(IResumeRepository resumes, IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _resumes = resumes;
        _variants = variants;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<ResumeVariantResponse>> ExecuteAsync(CreateResumeVariantCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var locale = command.Request.Locale?.Trim() ?? string.Empty;
        if (!ResumeVariantLocalization.IsValidLocale(locale))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "Local inválido. Use o formato BCP-47 (ex.: es-ES).", nameof(command.Request.Locale));

        var resume = await _resumes.GetByIdAsync(Id.Create(command.ResumeId), cancellationToken);
        if (resume.Status != EResultStatus.Success || resume.Data is null)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("Resume", command.ResumeId, "Currículo não encontrado.");
        if (resume.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("Resume", command.ResumeId, "Currículo não encontrado.");

        var existing = await _variants.ListByResumeAsync(Id.Create(command.ResumeId), cancellationToken);
        if (existing.Status == EResultStatus.Success && existing.Data!.Any(v => string.Equals(v.Locale, locale, StringComparison.OrdinalIgnoreCase)))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                $"Já existe uma variante {locale} deste currículo.", nameof(command.Request.Locale));

        var created = await _variants.CreateAsync(new ResumeVariant
        {
            Id = Id.Create(Guid.NewGuid()),
            ResumeId = Id.Create(command.ResumeId),
            CandidateId = Id.Create(command.CandidateId),
            Locale = locale,
            TenantId = "default"
        }, cancellationToken);
        if (created.Status != EResultStatus.Success)
            return RequestResult<ResumeVariantResponse>.FromResult(created);

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(created.Data!));
    }
}

/// <summary>Detalhe da variante com os overrides já desserializados.</summary>
public sealed class GetResumeVariantUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public GetResumeVariantUseCase(IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _variants = variants;
        _featureFlags = featureFlags;
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

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(variant.Data));
    }
}

/// <summary>Atualiza os textos localizados da variante — nunca o currículo base.</summary>
public sealed class UpdateResumeVariantUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public UpdateResumeVariantUseCase(IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _variants = variants;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<ResumeVariantResponse>> ExecuteAsync(UpdateResumeVariantCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(ResumeVariantLocalization.FeatureName, command.User, cancellationToken))
            return RequestResult<ResumeVariantResponse>.WithValidationError(
                "As variantes de currículo não estão habilitadas para esta conta.", "Feature");

        var variant = await _variants.GetByIdAsync(Id.Create(command.VariantId), cancellationToken);
        if (variant.Status != EResultStatus.Success || variant.Data is null)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");
        if (variant.Data.CandidateId.Value != command.CandidateId)
            return RequestResult<ResumeVariantResponse>.EntityNotFound("ResumeVariant", command.VariantId, "Variante de currículo não encontrada.");

        var errors = new List<RequestValidation>();
        var overrides = command.Request.Overrides ?? [];
        if (overrides.Count > ResumeVariantLocalization.MaxOverrides)
            errors.Add(new(nameof(command.Request.Overrides), $"Máximo de {ResumeVariantLocalization.MaxOverrides} campos localizados."));
        foreach (var (key, value) in overrides)
        {
            if (!ResumeVariantLocalization.IsValidKey(key))
                errors.Add(new(nameof(command.Request.Overrides), $"Chave de campo inválida: “{key}”."));
            else if ((value?.Length ?? 0) > ResumeVariantLocalization.MaxOverrideValueLength)
                errors.Add(new(nameof(command.Request.Overrides), $"Texto longo demais em “{key}”."));
        }
        if (errors.Count != 0)
            return RequestResult<ResumeVariantResponse>.WithValidations(errors);

        var entity = variant.Data;
        entity.OverridesJson = ResumeVariantLocalization.Serialize(
            overrides.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim()));

        var updated = await _variants.UpdateAsync(entity, cancellationToken);
        if (updated.Status != EResultStatus.Success)
            return RequestResult<ResumeVariantResponse>.FromResult(updated);

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(updated.Data!));
    }
}

/// <summary>Remove a variante (soft-delete) sem tocar o currículo base.</summary>
public sealed class DeleteResumeVariantUseCase
{
    private readonly IResumeVariantRepository _variants;
    private readonly IFeatureFlags _featureFlags;

    public DeleteResumeVariantUseCase(IResumeVariantRepository variants, IFeatureFlags featureFlags)
    {
        _variants = variants;
        _featureFlags = featureFlags;
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

        var deleted = await _variants.DeleteAsync(variant.Data.Id, cancellationToken);
        if (deleted.Status != EResultStatus.Success)
            return RequestResult<ResumeVariantResponse>.FromResult(deleted);

        return RequestResult<ResumeVariantResponse>.Success(ResumeVariantMapping.ToResponse(variant.Data));
    }
}
