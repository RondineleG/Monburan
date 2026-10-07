using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class UpdateResumeRequest
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsActive { get; set; }
    public List<EducationRequest>? EducationHistory { get; set; }
    public List<ExperienceRequest>? ExperienceHistory { get; set; }
    public List<LanguageRequest>? Languages { get; set; }
    public List<SkillRequest>? Skills { get; set; }
}
