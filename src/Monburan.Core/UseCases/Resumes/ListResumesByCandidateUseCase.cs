using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

public class ListResumesByCandidateUseCase : IUseCase<Guid, IEnumerable<ResumeResponse>>
{
    private readonly IResumeRepository _resumeRepository;

    public ListResumesByCandidateUseCase(IResumeRepository resumeRepository)
    {
        _resumeRepository = resumeRepository;
    }

    public async Task<RequestResult<IEnumerable<ResumeResponse>>> ExecuteAsync(Guid request, CancellationToken cancellationToken = default)
    {
        var result = await _resumeRepository.ListByCandidateAsync(request, cancellationToken: cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<IEnumerable<ResumeResponse>>.FromResult(result);

        var resumes = result.Data!
            .Select(MapToResponse)
            .OrderByDescending(r => r.CreatedAt);

        return RequestResult<IEnumerable<ResumeResponse>>.Success(resumes);
    }

    private static ResumeResponse MapToResponse(Resume r) => new()
    {
        Id = r.Id.Value,
        CandidateId = r.CandidateId.Value,
        Title = r.Title,
        Summary = r.Summary,
        IsActive = r.IsActive,
        SourceFileName = r.SourceFileName,
        SourceFileUrl = r.SourceFileUrl,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        EducationHistory = r.EducationHistory?.Select(e => new EducationResponse
        {
            Institution = e.Institution,
            Course = e.Course,
            Level = e.Level.ToString(),
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = e.Status.ToString()
        }).ToList(),
        ExperienceHistory = r.ExperienceHistory?.Select(e => new ExperienceResponse
        {
            Company = e.Company,
            Position = e.Position,
            Description = e.Description,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            IsCurrent = e.IsCurrent
        }).ToList(),
        Languages = r.Languages?.Select(l => new LanguageResponse
        {
            Language = l.Name,
            Level = l.Level.ToString()
        }).ToList(),
        Skills = r.Skills?.Select(s => new SkillResponse
        {
            Skill = s.Skill,
            Level = int.TryParse(s.Level, out var level) ? level : 0,
            YearsOfExperience = (int)s.YearsOfExperience
        }).ToList()
    };
}
