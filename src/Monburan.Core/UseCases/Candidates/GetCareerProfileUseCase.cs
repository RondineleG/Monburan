using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Candidates;

/// <summary>Perfil de carreira do candidato. Sem perfil, responde 200 com os campos nulos.</summary>
public class GetCareerProfileUseCase : IUseCase<Guid, CareerProfileResponse>
{
    private readonly ICareerProfileRepository _profiles;

    public GetCareerProfileUseCase(ICareerProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<RequestResult<CareerProfileResponse>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var result = await _profiles.GetByCandidateIdAsync(request, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<CareerProfileResponse>.FromResult(result);

        return RequestResult<CareerProfileResponse>.Success(CareerProfileMapper.ToResponse(result.Data));
    }
}
