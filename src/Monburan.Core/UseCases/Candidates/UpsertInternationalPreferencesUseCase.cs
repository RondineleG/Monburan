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

public sealed record UpsertInternationalPreferencesCommand(Guid CandidateId, InternationalPreferencesRequest Preferences);

/// <summary>Cria ou atualiza as preferências internacionais no perfil de carreira do candidato.</summary>
public sealed class UpsertInternationalPreferencesUseCase
{
    private const int MaxCountries = 20;
    private static readonly Regex CountryCode = new("^[A-Z]{2}$", RegexOptions.Compiled);

    private readonly ICareerProfileRepository _profiles;
    private readonly IFeatureFlags _featureFlags;

    public UpsertInternationalPreferencesUseCase(ICareerProfileRepository profiles, IFeatureFlags featureFlags)
    {
        _profiles = profiles;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<InternationalPreferencesResponse>> ExecuteAsync(
        UpsertInternationalPreferencesCommand command, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(InternationalCareerFeature.Name, user, cancellationToken))
            return RequestResult<InternationalPreferencesResponse>.WithValidationError(
                "As preferências internacionais não estão habilitadas para esta conta.", "Feature");

        var errors = new List<RequestValidation>();
        var request = command.Preferences;

        var countries = (request.InterestedCountries ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .ToList();
        if (countries.Any(code => !CountryCode.IsMatch(code)))
            errors.Add(new(nameof(request.InterestedCountries), "Países devem ser códigos ISO 3166-1 alfa-2 (ex.: ES, PT)."));
        if (countries.Count > MaxCountries)
            errors.Add(new(nameof(request.InterestedCountries), $"Informe no máximo {MaxCountries} países."));
        if (!Enum.IsDefined(request.Mobility))
            errors.Add(new(nameof(request.Mobility), "Disponibilidade inválida."));
        if (!Enum.IsDefined(request.WorkAuthorizationStatus))
            errors.Add(new(nameof(request.WorkAuthorizationStatus), "Situação de autorização inválida."));
        if (errors.Count != 0)
            return RequestResult<InternationalPreferencesResponse>.WithValidations(errors);

        var found = await _profiles.GetByCandidateIdAsync(command.CandidateId, cancellationToken);
        if (found.Status != EResultStatus.Success)
            return RequestResult<InternationalPreferencesResponse>.FromResult(found);

        var profile = found.Data;
        var isNew = profile is null;
        profile ??= new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(command.CandidateId),
            TenantId = "default"
        };

        profile.InterestedCountries = countries.Distinct().ToList();
        profile.Mobility = request.Mobility;
        profile.WorkAuthorizationStatus = request.WorkAuthorizationStatus;

        var result = isNew
            ? await _profiles.CreateAsync(profile, cancellationToken)
            : await _profiles.UpdateAsync(profile, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<InternationalPreferencesResponse>.FromResult(result);

        return RequestResult<InternationalPreferencesResponse>.Success(new InternationalPreferencesResponse
        {
            InterestedCountries = result.Data!.InterestedCountries,
            Mobility = result.Data.Mobility,
            WorkAuthorizationStatus = result.Data.WorkAuthorizationStatus
        });
    }
}
