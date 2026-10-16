using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
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
        bool? sortDescending = null);

    Task<PagedResponse<Job>> GetAllPagedAsync(int page, int size);
}
