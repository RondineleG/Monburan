using System.Security.Claims;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed record GetCandidateLanguagesCommand(Guid CandidateId);

/// <summary>Idiomas autodeclarados do candidato — coleção vazia quando ainda não preenchida.</summary>
public sealed class GetCandidateLanguagesUseCase
{
    private readonly ICandidateLanguageRepository _languages;
    private readonly IFeatureFlags _featureFlags;

    public GetCandidateLanguagesUseCase(ICandidateLanguageRepository languages, IFeatureFlags featureFlags)
    {
        _languages = languages;
        _featureFlags = featureFlags;
    }

    public async Task<RequestResult<CandidateLanguagesResponse>> ExecuteAsync(
        GetCandidateLanguagesCommand command, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!await _featureFlags.IsEnabledAsync(InternationalCareerFeature.Name, user, cancellationToken))
            return RequestResult<CandidateLanguagesResponse>.WithValidationError(
                "As preferências internacionais não estão habilitadas para esta conta.", "Feature");

        var result = await _languages.GetByCandidateIdAsync(Id.Create(command.CandidateId), cancellationToken);
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
}
