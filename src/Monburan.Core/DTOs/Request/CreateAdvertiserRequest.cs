using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class CreateAdvertiserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public EPersonType PersonType { get; set; }
    public string? CompanyDescription { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public Guid? AddressId { get; set; }
}
