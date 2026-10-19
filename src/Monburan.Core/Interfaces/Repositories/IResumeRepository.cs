using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

public interface IResumeRepository : IBaseRepository<Resume>
{
    Task<RequestResult<IEnumerable<Resume>>> ListByCandidateAsync(Guid candidateId);

    /// <summary>Currículo ativo do candidato com as habilidades carregadas (base das recomendações).</summary>
    Task<RequestResult<Resume?>> GetActiveWithSkillsAsync(Guid candidateId);
}
