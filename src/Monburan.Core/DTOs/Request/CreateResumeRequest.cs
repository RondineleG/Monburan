using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class CreateResumeRequest
{
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsActive { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceFileUrl { get; set; }
    public List<EducationRequest>? EducationHistory { get; set; }
    public List<ExperienceRequest>? ExperienceHistory { get; set; }
    public List<LanguageRequest>? Languages { get; set; }
    public List<SkillRequest>? Skills { get; set; }
}

public class EducationRequest
{
    public string Institution { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public EEducationLevel Level { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public EEducationStatus Status { get; set; }
}

public class ExperienceRequest
{
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class LanguageRequest
{
    public string Language { get; set; } = string.Empty;
    public ELanguageLevel Level { get; set; }
}

public class SkillRequest
{
    public string Skill { get; set; } = string.Empty;
    public int Level { get; set; }
    public int YearsOfExperience { get; set; }
}
