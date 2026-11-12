using System.Security.Claims;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Candidates;

internal static class InternationalCareerFeature
{
    public const string Name = "carreira-internacional";
}

public sealed record GetInternationalPreferencesCommand(Guid CandidateId);

/// <summary>Preferências internacionais do candidato — vazio quando o perfil ainda não foi preenchido.</summary>
public sealed class GetInternationalPreferencesUseCase
{
    private readonly ICareerProfileRepository _profiles;
    private readonly IFeatureFlags _featureFlags;

    public GetInternationalPreferencesUseCase(ICareerProfileRepository profiles, IFeatureFlags featureFlags)
    {
        _profiles = profiles;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<InternationalPreferencesResponse>> ExecuteAsync(
        GetInternationalPreferencesCommand command, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(InternationalCareerFeature.Name, user, cancellationToken))
            return RequestResult<InternationalPreferencesResponse>.WithValidationError(
                "As preferências internacionais não estão habilitadas para esta conta.", "Feature");

        var found = await _profiles.GetByCandidateIdAsync(command.CandidateId, cancellationToken);
        if (found.Status != EResultStatus.Success)
            return RequestResult<InternationalPreferencesResponse>.FromResult(found);

        var profile = found.Data;
        return RequestResult<InternationalPreferencesResponse>.Success(new InternationalPreferencesResponse
        {
            InterestedCountries = profile?.InterestedCountries ?? [],
            Mobility = profile?.Mobility ?? EInternationalMobility.NotInformed,
            WorkAuthorizationStatus = profile?.WorkAuthorizationStatus ?? EWorkAuthorizationStatus.NotInformed
        });
    }
}
