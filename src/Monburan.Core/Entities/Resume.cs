using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Resume : BaseEntity
{
    public virtual Candidate? Candidate { get; set; }

    [Required]
    public Id CandidateId { get; set; } = Id.Empty;

    public virtual ICollection<Education> EducationHistory { get; set; } = [];

    public virtual ICollection<Experience> ExperienceHistory { get; set; } = [];

    public bool IsActive { get; set; }

    public virtual ICollection<Language> Languages { get; set; } = [];

    public virtual ICollection<ResumeSkill> Skills { get; set; } = [];

    public string? Summary { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;
}