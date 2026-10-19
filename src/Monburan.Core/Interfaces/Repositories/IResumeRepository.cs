using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

public interface IResumeRepository : IBaseRepository<Resume>
{
    /// <summary>
    /// Currículos do candidato. <paramref name="size"/> em 0 devolve até o teto da listagem
    /// (<see cref="MaxListSize"/>); com tamanho maior, devolve a página pedida.
    /// </summary>
    Task<RequestResult<IEnumerable<Resume>>> ListByCandidateAsync(Guid candidateId, int page = 1, int size = 0);

    /// <summary>Currículo ativo do candidato com as habilidades carregadas (base das recomendações).</summary>
    Task<RequestResult<Resume?>> GetActiveWithSkillsAsync(Guid candidateId);

    /// <summary>Quantidade de currículos cadastrados (não excluídos) do candidato.</summary>
    Task<RequestResult<int>> CountByCandidateAsync(Guid candidateId);

    /// <summary>
    /// Maior quantidade que uma listagem devolve sem paginação. Um candidato com dezenas de
    /// milhares de currículos não pode materializar tudo em memória a cada requisição.
    /// </summary>
    const int MaxListSize = 200;
}
