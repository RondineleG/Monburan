using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Advertiser : BaseEntity
{
    public string? ActivePlan { get; set; }

    public Address? Address { get; set; }

    public Id? AddressId { get; set; }

    public string? CompanyDescription { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; } = false;

    public bool IsPhoneVerified { get; set; } = false;

    public virtual ICollection<Job> Jobs { get; set; } = [];

    public string? LogoUrl { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public EPersonType PersonType { get; set; }

    public string? Phone { get; set; }

    public EAdvertiserStatus Status { get; set; } = EAdvertiserStatus.Incomplete;

    public string? TaxId { get; set; }

    public string? Website { get; set; }
}