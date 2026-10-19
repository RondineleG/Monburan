using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Admin;

/// <summary>Vagas de todas as empresas, em qualquer situação, para a moderação da administração.</summary>
public sealed class AdminListJobsUseCase : IUseCase<AdminListJobsRequest, AdminListJobsResponse>
{
    private readonly IJobRepository _jobs;
    private readonly TimeProvider _time;

    public AdminListJobsUseCase(IJobRepository jobs, TimeProvider time)
    {
        _jobs = jobs;
        _time = time;
    }

    public async Task<RequestResult<AdminListJobsResponse>> ExecuteAsync(AdminListJobsRequest request)
    {
        var page = Math.Max(1, request.Page);
        var size = request.Size is < 1 or > 100 ? 20 : request.Size;
        EJobStatus? status = Enum.TryParse<EJobStatus>(request.Status, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        var today = _time.GetUtcNow().UtcDateTime.Date;

        var found = await _jobs.AdminSearchAsync(request.SearchTerm, status, request.AdvertiserId, page, size);
        var counts = await _jobs.CountByStatusAsync();

        return RequestResult<AdminListJobsResponse>.Success(new AdminListJobsResponse
        {
            Items = found.Data.Select(job => AdminJobMapper.ToResponse(job, today)).ToList(),
            TotalCount = found.TotalCount,
            Page = page,
            Size = size,
            StatusCounts = Enum.GetValues<EJobStatus>().ToDictionary(item => item.ToString(), item => counts.GetValueOrDefault(item))
        });
    }
}
