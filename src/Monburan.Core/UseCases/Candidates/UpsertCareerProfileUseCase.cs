using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

public sealed record UpsertCareerProfileCommand(Guid CandidateId, CareerProfileRequest Profile);

/// <summary>Cria ou atualiza o perfil de carreira do candidato (um por candidato).</summary>
public class UpsertCareerProfileUseCase : IUseCase<UpsertCareerProfileCommand, CareerProfileResponse>
{
    private readonly ICareerProfileRepository _profiles;

    public UpsertCareerProfileUseCase(ICareerProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<RequestResult<CareerProfileResponse>> ExecuteAsync(UpsertCareerProfileCommand request, CancellationToken cancellationToken = default)
    {
        var errors = CareerProfileInputValidation.Validate(request.Profile);
        if (errors.Count != 0)
            return RequestResult<CareerProfileResponse>.WithValidations(errors);

        var found = await _profiles.GetByCandidateIdAsync(request.CandidateId, cancellationToken);
        if (found.Status != EResultStatus.Success)
            return RequestResult<CareerProfileResponse>.FromResult(found);

        var profile = found.Data;
        var isNew = profile is null;
        profile ??= new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(request.CandidateId),
            TenantId = "default"
        };

        profile.DesiredRole = request.Profile.DesiredRole?.Trim();
        profile.City = request.Profile.City?.Trim();
        profile.State = request.Profile.State?.Trim().ToUpperInvariant();
        profile.Mode = JobModes.Normalize(request.Profile.Mode);
        profile.Category = request.Profile.Category?.Trim();
        profile.MinimumSalary = request.Profile.MinimumSalary;
        profile.ContractType = request.Profile.ContractType;

        var result = isNew
            ? await _profiles.CreateAsync(profile, cancellationToken)
            : await _profiles.UpdateAsync(profile, cancellationToken);

        if (result.Status != EResultStatus.Success)
            return RequestResult<CareerProfileResponse>.FromResult(result);

        return RequestResult<CareerProfileResponse>.Success(CareerProfileMapper.ToResponse(result.Data!));
    }
}
