using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Candidate : BaseEntity, IHasActivePlan
{
    public string? ActivePlan { get; set; }

    public Address? Address { get; set; }

    public Id? AddressId { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = [];

    public DateTime? DateOfBirth { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; } = false;

    public bool IsPhoneVerified { get; set; } = false;

    public string? LinkedIn { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public virtual ICollection<Resume> Resumes { get; set; } = [];

    public EAccountStatus Status { get; set; } = EAccountStatus.Incomplete;
}