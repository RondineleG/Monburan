using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class CareerProfileRequest
{
    public string? DesiredRole { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    /// <summary>"OnSite", "Remote" ou "Hybrid".</summary>
    public string? Mode { get; set; }

    public string? Category { get; set; }

    public decimal? MinimumSalary { get; set; }

    public EContractType? ContractType { get; set; }
}
