using Monburan.Core.Entities;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Perfil de carreira do candidato (um por candidato).</summary>
public interface ICareerProfileRepository
{
    /// <summary>Sucesso com <c>null</c> quando o candidato ainda não tem perfil.</summary>
    Task<RequestResult<CareerProfile?>> GetByCandidateIdAsync(Guid candidateId);

    Task<RequestResult<CareerProfile>> CreateAsync(CareerProfile entity);

    Task<RequestResult<CareerProfile>> UpdateAsync(CareerProfile entity);
}
