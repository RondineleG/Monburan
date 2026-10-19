using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Response;

public class CareerProfileResponse
{
    public string? DesiredRole { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Mode { get; set; }

    public string? Category { get; set; }

    public decimal? MinimumSalary { get; set; }

    public EContractType? ContractType { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
