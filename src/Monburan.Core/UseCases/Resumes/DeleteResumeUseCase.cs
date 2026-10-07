using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

public class DeleteResumeUseCase : IUseCase<Guid, ResumeResponse>
{
    private readonly IBaseRepository<Resume> _resumeRepository;

    public DeleteResumeUseCase(IBaseRepository<Resume> resumeRepository)
    {
        _resumeRepository = resumeRepository;
    }

    public async Task<RequestResult<ResumeResponse>> ExecuteAsync(Guid resumeId)
    {
        var existingResumeResult = await _resumeRepository.GetByIdAsync(Id.Create(resumeId));
        if (existingResumeResult.Status != EResultStatus.Success || existingResumeResult.Data == null)
            return RequestResult<ResumeResponse>.WithError("Currículo não encontrado.");

        var existingResume = existingResumeResult.Data;

        // Soft delete: desativa o currículo
        existingResume.IsActive = false;

        var result = await _resumeRepository.UpdateAsync(existingResume);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ResumeResponse>.WithError(result.Message);

        return RequestResult<ResumeResponse>.Success(MapToResponse(result.Data!));
    }

    private static ResumeResponse MapToResponse(Resume r) => new()
    {
        Id = r.Id.Value,
        CandidateId = r.CandidateId.Value,
        Title = r.Title,
        Summary = r.Summary,
        IsActive = r.IsActive,
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
