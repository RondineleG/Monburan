namespace Monburan.Core.DTOs.Response;

public class JobResponse
{
    public Guid Id { get; set; }
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
    public DateTime ExpirationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Location
    public string? City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "BR";
    public string Type { get; set; } = "OnSite";
    public bool IsRemote { get; set; }
    
    // Salary
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string SalaryCurrency { get; set; } = "BRL";
    public bool SalaryDisplay { get; set; } = true;
    
    // Counters
    public int Views { get; set; }
    public int Applications { get; set; }
    
    // Advertiser
    public Guid AdvertiserId { get; set; }
    public string? AdvertiserName { get; set; }
    public string? AdvertiserLogoUrl { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
