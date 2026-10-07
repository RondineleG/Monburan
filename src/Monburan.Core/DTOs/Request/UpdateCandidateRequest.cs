namespace Monburan.Core.DTOs.Request;

public class UpdateCandidateRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? LinkedIn { get; set; }
    public Guid? AddressId { get; set; }
}
