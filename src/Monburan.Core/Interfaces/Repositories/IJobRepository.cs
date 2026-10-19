using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

public interface IJobRepository : IBaseRepository<Job>
{
    Task<RequestResult<PagedResponse<Job>>> SearchAsync(
        string? searchTerm = null,
        string? city = null,
        string? state = null,
        bool? isRemote = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        string? category = null,
        Guid? advertiserId = null,
        int page = 1,
        int size = 10,
        string? type = null,
        string? sortBy = null,
        bool? sortDescending = null,
        EContractType? contractType = null,
        ESalaryPeriod? salaryPeriod = null,
        int? publishedWithinDays = null);

    Task<PagedResponse<Job>> GetAllPagedAsync(int page, int size);

    /// <summary>
    /// Visão da administração: vagas de qualquer empresa e em qualquer situação (menos as excluídas), mais recentes primeiro.
    /// A busca olha o título, a categoria e o nome da empresa.
    /// </summary>
    Task<PagedResponse<Job>> AdminSearchAsync(string? searchTerm, EJobStatus? status, Guid? advertiserId, int page, int size);

    /// <summary>Quantidade de vagas (não excluídas) por situação.</summary>
    Task<IReadOnlyDictionary<EJobStatus, int>> CountByStatusAsync();

    /// <summary>Quantidade de vagas ativas (não excluídas) do anunciante — base do limite do plano.</summary>
    Task<int> CountActiveByAdvertiserAsync(Guid advertiserId);

    /// <summary>Vagas ativas e não expiradas, mais recentes primeiro — candidatas às recomendações.</summary>
    Task<RequestResult<IEnumerable<Job>>> ListActiveForRecommendationsAsync();

    /// <summary>Incrementa o contador de visualizações de forma atômica. <c>false</c> quando a vaga não existe.</summary>
    Task<RequestResult<bool>> IncrementViewsAsync(Guid jobId);
}
