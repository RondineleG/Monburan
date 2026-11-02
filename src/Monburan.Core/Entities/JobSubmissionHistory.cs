using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Cada mudança de situação da sugestão, com quem decidiu e por quê.</summary>
public class JobSubmissionHistory : BaseEntity
{
    public Id SubmissionId { get; set; } = null!;

    public virtual JobSubmission? Submission { get; set; }

    public ESubmissionStatus Status { get; set; }

    /// <summary>Identificador de quem agiu; nulo quando foi o próprio envio.</summary>
    public string? ActorId { get; set; }

    public string? Note { get; set; }
}
