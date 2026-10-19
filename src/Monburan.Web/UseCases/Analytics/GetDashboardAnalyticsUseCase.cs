using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.UseCases.Analytics;

public class GetDashboardAnalyticsUseCase
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public GetDashboardAnalyticsUseCase(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<DashboardAnalyticsResponse>> ExecuteAsync(Guid? advertiserId = null)
    {
        if (!advertiserId.HasValue)
            return RequestResult<DashboardAnalyticsResponse>.WithError("O painel de recrutamento exige um perfil de anunciante.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertiserKey = Id.Create(advertiserId.Value);

        // Agregações feitas no banco: antes todas as candidaturas (com Candidate e Job)
        // eram materializadas em memória só para contar
        var jobs = context.Jobs
            .AsNoTracking()
            .Where(j => !j.IsDeleted && j.AdvertiserId == advertiserKey);

        var applications = context.Applications
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.Job != null && !a.Job.IsDeleted && a.Job.AdvertiserId == advertiserKey);

        var jobsByStatus = await jobs
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var applicationsByStatus = await applications
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var response = new DashboardAnalyticsResponse
        {
            JobsByStatus = jobsByStatus.ToDictionary(g => g.Status.ToString(), g => g.Count),
            ApplicationsByStatus = applicationsByStatus.ToDictionary(g => g.Status.ToString(), g => g.Count),
            TotalAdvertisers = 1,
            TotalCandidates = await applications.Select(a => a.CandidateId).Distinct().CountAsync()
        };

        response.TotalJobs = response.JobsByStatus.Values.Sum();
        response.ActiveJobs = response.JobsByStatus.GetValueOrDefault(nameof(EJobStatus.Active));
        response.TotalApplications = response.ApplicationsByStatus.Values.Sum();

        var recentJobs = await jobs
            .Include(j => j.Advertiser)
            .OrderByDescending(j => j.CreatedAt)
            .Take(6)
            .ToListAsync();
        response.RecentJobs = recentJobs.Select(job => JobResponseMapper.ToResponse(job)).ToList();

        var recentApplications = await applications
            .Include(a => a.Candidate)
            .Include(a => a.Job)
            .OrderByDescending(a => a.AppliedAt ?? a.CreatedAt)
            .Take(6)
            .ToListAsync();
        response.RecentApplications = recentApplications.Select(MapToApplicationResponse).ToList();

        return RequestResult<DashboardAnalyticsResponse>.Success(response);
    }

    private static ApplicationResponse MapToApplicationResponse(Application application)
    {
        return new ApplicationResponse
        {
            Id = application.Id.Value,
            CandidateId = application.CandidateId.Value,
            CandidateName = application.Candidate?.Name,
            CandidateEmail = application.Candidate?.Email,
            JobId = application.JobId.Value,
            JobTitle = application.Job?.Title,
            Status = application.Status.ToString(),
            AppliedAt = application.AppliedAt,
            ViewedAt = application.ViewedAt,
            SelectedAt = application.SelectedAt,
            RejectedAt = application.RejectedAt,
            Notes = application.Notes,
            CreatedAt = application.CreatedAt
        };
    }
}
