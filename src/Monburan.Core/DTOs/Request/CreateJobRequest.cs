using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class CreateJobRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public string? Responsibilities { get; set; }
    public string? Benefits { get; set; }
    public string? ApplicationInstructions { get; set; }
    public int NumberOfOpenings { get; set; } = 1;
    public string? Observations { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public EContractType? ContractType { get; set; }
    public DateTime ExpirationDate { get; set; }
    
    // Location
    public string? City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "BR";
    public string Type { get; set; } = "OnSite";
    public bool IsRemote { get; set; }
    public bool ShowAddress { get; set; } = true;
    
    // Salary
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public ESalaryPeriod? SalaryPeriod { get; set; }
    public string SalaryCurrency { get; set; } = "BRL";
    public bool SalaryDisplay { get; set; } = true;
    
    public Guid AdvertiserId { get; set; }
}
