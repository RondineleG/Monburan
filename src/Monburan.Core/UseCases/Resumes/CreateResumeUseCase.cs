using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

public class CreateResumeUseCase : IUseCase<CreateResumeRequest, ResumeResponse>
{
    private readonly IBaseRepository<Resume> _resumeRepository;
    private readonly IEntityValidator<Resume> _validator;

    public CreateResumeUseCase(
        IBaseRepository<Resume> resumeRepository,
        IEntityValidator<Resume> validator)
    {
        _resumeRepository = resumeRepository;
        _validator = validator;
    }

    public async Task<RequestResult<ResumeResponse>> ExecuteAsync(CreateResumeRequest request, CancellationToken cancellationToken = default)
    {
        var resume = new Resume
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(request.CandidateId),
            Title = request.Title,
            Summary = request.Summary,
            IsActive = request.IsActive,
            SourceFileName = request.SourceFileName,
            SourceFileUrl = request.SourceFileUrl,
            TenantId = "default",
            EducationHistory = MapEducationHistory(request.EducationHistory),
            ExperienceHistory = MapExperienceHistory(request.ExperienceHistory),
            Languages = MapLanguages(request.Languages),
            Skills = MapSkills(request.Skills)
        };

        var validationResult = _validator.Validate(resume);
        if (!validationResult.IsValid)
            return RequestResult<ResumeResponse>.WithValidationError(validationResult.Errors.First().Message);

        var result = await _resumeRepository.CreateAsync(resume, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ResumeResponse>.FromResult(result);

        return RequestResult<ResumeResponse>.Success(MapToResponse(result.Data!));
    }

    private static ICollection<Education> MapEducationHistory(List<EducationRequest>? requests)
    {
        if (requests == null) return [];

        return requests.Select(r => new Education
        {
            Institution = r.Institution,
            Course = r.Course,
            Level = r.Level,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Status = r.Status
        }).ToList();
    }

    private static ICollection<Experience> MapExperienceHistory(List<ExperienceRequest>? requests)
    {
        if (requests == null) return [];

        return requests.Select(r => new Experience
        {
            Company = r.Company,
            Position = r.Position,
            Description = r.Description,
            StartDate = r.StartDate ?? DateTime.MinValue,
            EndDate = r.EndDate,
            IsCurrent = r.IsCurrent
        }).ToList();
    }

    private static ICollection<Language> MapLanguages(List<LanguageRequest>? requests)
    {
        if (requests == null) return [];

        return requests.Select(r => new Language
        {
            Name = r.Language,
            Level = r.Level
        }).ToList();
    }

    private static ICollection<ResumeSkill> MapSkills(List<SkillRequest>? requests)
    {
        if (requests == null) return [];

        return requests.Select(r => new ResumeSkill
        {
            Skill = r.Skill,
            Level = r.Level.ToString(),
            YearsOfExperience = r.YearsOfExperience
        }).ToList();
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
