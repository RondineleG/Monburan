using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.Candidates;

internal static class CareerProfileMapper
{
    public static CareerProfileResponse ToResponse(CareerProfile? profile) => new()
    {
        DesiredRole = profile?.DesiredRole,
        City = profile?.City,
        State = profile?.State,
        Mode = profile?.Mode,
        Category = profile?.Category,
        MinimumSalary = profile?.MinimumSalary,
        ContractType = profile?.ContractType,
        UpdatedAt = profile?.UpdatedAt
    };
}
