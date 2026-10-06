using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Application : BaseEntity
{
    public DateTime? AppliedAt { get; set; }

    public virtual Candidate? Candidate { get; set; }

    [Required]
    public Id CandidateId { get; set; } = Id.Empty;

    public virtual Job? Job { get; set; }

    [Required]
    public Id JobId { get; set; } = Id.Empty;

    public string? Notes { get; set; }

    public DateTime? RejectedAt { get; set; }

    public DateTime? SelectedAt { get; set; }

    public EApplicationStatus Status { get; set; } = EApplicationStatus.Sent;

    public virtual ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = [];

    public DateTime? ViewedAt { get; set; }
}