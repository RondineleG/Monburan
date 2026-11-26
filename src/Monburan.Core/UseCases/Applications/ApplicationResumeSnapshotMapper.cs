using System.Text.Json;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.Applications;

public static class ApplicationResumeSnapshotMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(Resume resume)
    {
        var snapshot = new ApplicationResumeSnapshot
        {
            ResumeId = resume.Id.Value,
            Title = resume.Title,
            Summary = resume.Summary,
            IsActive = resume.IsActive,
            SourceFileName = resume.SourceFileName,
            SourceFileUrl = resume.SourceFileUrl,
            CapturedAtUtc = DateTime.UtcNow,
            EducationHistory = resume.EducationHistory.Select(e => new ApplicationResumeEducationSnapshot
            {
                Institution = e.Institution,
                Course = e.Course,
                Level = e.Level.ToString(),
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Status = e.Status.ToString()
            }).ToList(),
            ExperienceHistory = resume.ExperienceHistory.Select(e => new ApplicationResumeExperienceSnapshot
            {
                Company = e.Company,
                Position = e.Position,
                Description = e.Description,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                IsCurrent = e.IsCurrent
            }).ToList(),
            Languages = resume.Languages.Select(l => new ApplicationResumeLanguageSnapshot
            {
                Language = l.Name,
                Level = l.Level.ToString()
            }).ToList(),
            Skills = resume.Skills.Select(s => new ApplicationResumeSkillSnapshot
            {
                Skill = s.Skill,
                Level = s.Level,
                YearsOfExperience = s.YearsOfExperience
            }).ToList()
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static ApplicationResumeSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ApplicationResumeSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
