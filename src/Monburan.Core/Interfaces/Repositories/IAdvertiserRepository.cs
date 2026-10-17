using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

public interface IAdvertiserRepository : IBaseRepository<Advertiser>
{
    /// <summary>Busca paginada com filtros aplicados no banco (TotalCount é o total filtrado).</summary>
    Task<RequestResult<PagedResponse<Advertiser>>> SearchAsync(string? searchTerm, EAdvertiserStatus? status, int page, int size);
}
