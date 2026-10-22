using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Jobs;

/// <summary>
/// Vagas recomendadas para o candidato: só ativas, não expiradas e em que ele ainda não se candidatou,
/// ordenadas pela pontuação de <see cref="JobRecommendationScorer"/>. Sem perfil e sem habilidades, devolve lista vazia.
/// </summary>
public class ListRecommendedJobsUseCase : IUseCase<Guid, ListJobsResponse>
{
    public const int MaxResults = 20;

    private readonly IJobRepository _jobRepository;
    private readonly ICareerProfileRepository _careerProfiles;
    private readonly IResumeRepository _resumes;
    private readonly IApplicationRepository _applications;

    public ListRecommendedJobsUseCase(
        IJobRepository jobRepository,
        ICareerProfileRepository careerProfiles,
        IResumeRepository resumes,
        IApplicationRepository applications)
    {
        _jobRepository = jobRepository;
        _careerProfiles = careerProfiles;
        _resumes = resumes;
        _applications = applications;
    }

    public async Task<RequestResult<ListJobsResponse>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var profileResult = await _careerProfiles.GetByCandidateIdAsync(request, cancellationToken);
        if (profileResult.Status != EResultStatus.Success)
            return RequestResult<ListJobsResponse>.FromResult(profileResult);

        var resumeResult = await _resumes.GetActiveWithSkillsAsync(request, cancellationToken);
        if (resumeResult.Status != EResultStatus.Success)
            return RequestResult<ListJobsResponse>.FromResult(resumeResult);

        var skills = resumeResult.Data?.Skills
            .Select(skill => skill.Skill)
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .ToList() ?? [];

        var profile = profileResult.Data;
        if (profile is null && skills.Count == 0)
            return RequestResult<ListJobsResponse>.Success(EmptyResponse());

        var jobsResult = await _jobRepository.ListActiveForRecommendationsAsync(cancellationToken);
        if (jobsResult.Status != EResultStatus.Success || jobsResult.Data is null)
            return RequestResult<ListJobsResponse>.FromResult(jobsResult);

        var applicationsResult = await _applications.ListByCandidateAsync(request, cancellationToken: cancellationToken);
        if (applicationsResult.Status != EResultStatus.Success || applicationsResult.Data is null)
            return RequestResult<ListJobsResponse>.FromResult(applicationsResult);

        var appliedJobIds = applicationsResult.Data.Select(application => application.JobId.Value).ToHashSet();

        var jobs = jobsResult.Data
            .Where(job => !appliedJobIds.Contains(job.Id.Value))
            .Select(job => new { Job = job, Score = JobRecommendationScorer.Score(job, profile, skills) })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Job.CreatedAt)
            .Take(MaxResults)
            .Select(item => JobResponseMapper.ToResponse(item.Job))
            .ToList();

        return RequestResult<ListJobsResponse>.Success(new ListJobsResponse
        {
            Jobs = jobs,
            TotalCount = jobs.Count,
            Page = 1,
            Size = MaxResults
        });
    }

    private static ListJobsResponse EmptyResponse() => new()
    {
        Jobs = [],
        TotalCount = 0,
        Page = 1,
        Size = MaxResults
    };
}
