using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Candidates;

/// <summary>Progresso do candidato: completude do perfil (itens de peso igual), candidaturas por situação, vagas salvas e alertas ativos.</summary>
public class GetCandidateProgressUseCase : IUseCase<Guid, CandidateProgressResponse>
{
    private const string ProfileActionUrl = "/perfil/candidato";
    private const string ResumeActionUrl = "/curriculos";
    private const string CareerProfileActionUrl = "/perfil-profissional";

    private readonly ICandidateRepository _candidates;
    private readonly IResumeRepository _resumes;
    private readonly ICareerProfileRepository _profiles;
    private readonly IApplicationRepository _applications;
    private readonly ISavedJobRepository _savedJobs;
    private readonly IJobAlertRepository _jobAlerts;

    public GetCandidateProgressUseCase(
        ICandidateRepository candidates,
        IResumeRepository resumes,
        ICareerProfileRepository profiles,
        IApplicationRepository applications,
        ISavedJobRepository savedJobs,
        IJobAlertRepository jobAlerts)
    {
        _candidates = candidates;
        _resumes = resumes;
        _profiles = profiles;
        _applications = applications;
        _savedJobs = savedJobs;
        _jobAlerts = jobAlerts;
    }

    public async Task<RequestResult<CandidateProgressResponse>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var candidateResult = await _candidates.GetByIdAsync(Id.Create(request), cancellationToken);
        if (candidateResult.Status != EResultStatus.Success || candidateResult.Data is null)
            return RequestResult<CandidateProgressResponse>.EntityNotFound("Candidate", request, "Candidato não encontrado.");

        var candidate = candidateResult.Data;

        var resumeCountResult = await _resumes.CountByCandidateAsync(request, cancellationToken);
        if (resumeCountResult.Status != EResultStatus.Success)
            return RequestResult<CandidateProgressResponse>.FromResult(resumeCountResult);

        var profileResult = await _profiles.GetByCandidateIdAsync(request, cancellationToken);
        if (profileResult.Status != EResultStatus.Success)
            return RequestResult<CandidateProgressResponse>.FromResult(profileResult);

        var countsResult = await _applications.CountByStatusForCandidateAsync(request, cancellationToken);
        if (countsResult.Status != EResultStatus.Success || countsResult.Data is null)
            return RequestResult<CandidateProgressResponse>.FromResult(countsResult);

        var savedResult = await _savedJobs.CountByCandidateAsync(request, cancellationToken);
        if (savedResult.Status != EResultStatus.Success)
            return RequestResult<CandidateProgressResponse>.FromResult(savedResult);

        var alertsResult = await _jobAlerts.CountActiveByCandidateAsync(request, cancellationToken);
        if (alertsResult.Status != EResultStatus.Success)
            return RequestResult<CandidateProgressResponse>.FromResult(alertsResult);

        var checklist = BuildChecklist(candidate, resumeCountResult.Data, profileResult.Data);
        var doneCount = checklist.Count(item => item.Done);

        return RequestResult<CandidateProgressResponse>.Success(new CandidateProgressResponse
        {
            CompletenessPercent = (int)Math.Round(doneCount * 100.0 / checklist.Count),
            Checklist = checklist,
            Applications = new CandidateApplicationCounts
            {
                Total = countsResult.Data.Values.Sum(),
                Sent = CountOf(countsResult.Data, EApplicationStatus.Sent),
                Viewed = CountOf(countsResult.Data, EApplicationStatus.Viewed),
                Selected = CountOf(countsResult.Data, EApplicationStatus.Selected),
                Rejected = CountOf(countsResult.Data, EApplicationStatus.Rejected),
                Archived = CountOf(countsResult.Data, EApplicationStatus.Archived)
            },
            SavedJobs = savedResult.Data,
            ActiveAlerts = alertsResult.Data
        });
    }

    private static List<CandidateProgressChecklistItem> BuildChecklist(Candidate candidate, int resumeCount, CareerProfile? profile)
    {
        var hasCareerProfile = profile is not null
            && (!string.IsNullOrWhiteSpace(profile.DesiredRole) || !string.IsNullOrWhiteSpace(profile.City));

        return
        [
            Item("name", "Nome completo", !string.IsNullOrWhiteSpace(candidate.Name), ProfileActionUrl),
            Item("phone", "Telefone", !string.IsNullOrWhiteSpace(candidate.Phone), ProfileActionUrl),
            Item("resume", "Currículo cadastrado", resumeCount >= 1, ResumeActionUrl),
            Item("careerProfile", "Perfil de carreira", hasCareerProfile, CareerProfileActionUrl),
            Item("linkedin", "LinkedIn", !string.IsNullOrWhiteSpace(candidate.LinkedIn), ProfileActionUrl),
            Item("address", "Endereço", candidate.AddressId is not null, ProfileActionUrl)
        ];
    }

    private static CandidateProgressChecklistItem Item(string key, string label, bool done, string actionUrl) =>
        new() { Key = key, Label = label, Done = done, ActionUrl = actionUrl };

    private static int CountOf(IReadOnlyDictionary<EApplicationStatus, int> counts, EApplicationStatus status) =>
        counts.TryGetValue(status, out var count) ? count : 0;
}
