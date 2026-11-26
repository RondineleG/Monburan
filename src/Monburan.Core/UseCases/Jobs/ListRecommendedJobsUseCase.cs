using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Jobs;

/// <summary>
/// Vagas recomendadas para o candidato: só ativas, não expiradas e em que ele ainda não se candidatou,
/// ordenadas pela pontuação de <see cref="JobRecommendationScorer"/>. Sem dados de personalização, usa ordenação básica por recência.
/// </summary>
public class ListRecommendedJobsUseCase : IUseCase<Guid, ListJobsResponse>
{
    public const int MaxResults = 20;
    public const string ExplainableRecommendationFeature = "recomendacao-explicavel";

    private readonly IJobRepository _jobRepository;
    private readonly ICareerProfileRepository _careerProfiles;
    private readonly IResumeRepository _resumes;
    private readonly IApplicationRepository _applications;
    private readonly IFeatureFlags _featureFlags;
    private readonly ICurrentPrincipalAccessor _currentPrincipal;

    public ListRecommendedJobsUseCase(
        IJobRepository jobRepository,
        ICareerProfileRepository careerProfiles,
        IResumeRepository resumes,
        IApplicationRepository applications,
        IFeatureFlags featureFlags,
        ICurrentPrincipalAccessor currentPrincipal)
    {
        _jobRepository = jobRepository;
        _careerProfiles = careerProfiles;
        _resumes = resumes;
        _applications = applications;
        _featureFlags = featureFlags;
        _currentPrincipal = currentPrincipal;
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
        var hasRecommendationInput = HasRecommendationInput(profile, skills);

        var jobsResult = await _jobRepository.ListActiveForRecommendationsAsync(cancellationToken);
        if (jobsResult.Status != EResultStatus.Success || jobsResult.Data is null)
            return RequestResult<ListJobsResponse>.FromResult(jobsResult);

        var applicationsResult = await _applications.ListByCandidateAsync(request, cancellationToken: cancellationToken);
        if (applicationsResult.Status != EResultStatus.Success || applicationsResult.Data is null)
            return RequestResult<ListJobsResponse>.FromResult(applicationsResult);

        var appliedJobIds = applicationsResult.Data.Select(application => application.JobId.Value).ToHashSet();

        var explain = await _featureFlags.IsEnabledAsync(
            ExplainableRecommendationFeature, _currentPrincipal.User, cancellationToken);

        var candidates = jobsResult.Data
            .Where(job => !appliedJobIds.Contains(job.Id.Value))
            .Select(job => new RankedJob(job, JobRecommendationScorer.Evaluate(job, profile, skills)))
            .ToList();

        IEnumerable<RankedJob> ranked = hasRecommendationInput
            ? candidates
                .Where(item => item.Evaluation.Score > 0)
                .OrderByDescending(item => item.Evaluation.Score)
                .ThenByDescending(item => item.Job.CreatedAt)
                .ThenBy(item => item.Job.Id.Value)
            : candidates
                .OrderByDescending(item => item.Job.CreatedAt)
                .ThenBy(item => item.Job.Id.Value);

        var jobs = ranked
            .Take(MaxResults)
            .Select(item =>
            {
                var response = JobResponseMapper.ToResponse(item.Job);
                if (explain)
                {
                    response.RecommendationScore = item.Evaluation.Score;
                    response.RecommendationFactors = item.Evaluation.Factors;
                }
                return response;
            })
            .ToList();

        return RequestResult<ListJobsResponse>.Success(new ListJobsResponse
        {
            Jobs = jobs,
            TotalCount = jobs.Count,
            Page = 1,
            Size = MaxResults
        });
    }

    private static bool HasRecommendationInput(CareerProfile? profile, IReadOnlyCollection<string> skills) =>
        skills.Count > 0
        || profile is not null && (
            !string.IsNullOrWhiteSpace(profile.DesiredRole)
            || !string.IsNullOrWhiteSpace(profile.Category)
            || !string.IsNullOrWhiteSpace(profile.City)
            || !string.IsNullOrWhiteSpace(profile.State)
            || !string.IsNullOrWhiteSpace(profile.Mode)
            || profile.MinimumSalary is > 0);

    private sealed record RankedJob(Job Job, JobRecommendationScorer.Evaluation Evaluation);
}
