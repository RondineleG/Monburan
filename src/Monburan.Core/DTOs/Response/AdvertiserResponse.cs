namespace Monburan.Core.DTOs.Response;

public class AdvertiserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public string PersonType { get; set; } = string.Empty;
    public string? CompanyDescription { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? ActivePlan { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int JobsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
