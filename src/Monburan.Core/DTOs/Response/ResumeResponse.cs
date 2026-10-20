namespace Monburan.Core.DTOs.Response;

public class ResumeResponse
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsActive { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceFileUrl { get; set; }
    public List<EducationResponse>? EducationHistory { get; set; }
    public List<ExperienceResponse>? ExperienceHistory { get; set; }
    public List<LanguageResponse>? Languages { get; set; }
    public List<SkillResponse>? Skills { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EducationResponse
{
    public string Institution { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ExperienceResponse
{
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class LanguageResponse
{
    public string Language { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}

public class SkillResponse
{
    public string Skill { get; set; } = string.Empty;
    public int Level { get; set; }
    public int YearsOfExperience { get; set; }
}
