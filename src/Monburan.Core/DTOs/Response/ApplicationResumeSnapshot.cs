namespace Monburan.Core.DTOs.Response;

/// <summary>Conteúdo do currículo no momento da candidatura; não acompanha edições posteriores.</summary>
public class ApplicationResumeSnapshot
{
    public Guid ResumeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsActive { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceFileUrl { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public List<ApplicationResumeEducationSnapshot> EducationHistory { get; set; } = [];
    public List<ApplicationResumeExperienceSnapshot> ExperienceHistory { get; set; } = [];
    public List<ApplicationResumeLanguageSnapshot> Languages { get; set; } = [];
    public List<ApplicationResumeSkillSnapshot> Skills { get; set; } = [];
}

public class ApplicationResumeEducationSnapshot
{
    public string Institution { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ApplicationResumeExperienceSnapshot
{
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class ApplicationResumeLanguageSnapshot
{
    public string Language { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}

public class ApplicationResumeSkillSnapshot
{
    public string Skill { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public float YearsOfExperience { get; set; }
}
