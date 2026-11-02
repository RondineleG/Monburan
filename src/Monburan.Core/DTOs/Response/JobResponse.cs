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

    /// <summary>Tipo de contratação ("Clt", "Pj"...); nulo quando a vaga não informa.</summary>
    public string? ContractType { get; set; }

    public string? Tags { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Motivo da moderação, só com a vaga oculta ou rejeitada (e só quem a vê é a empresa dona).</summary>
    public string? ModerationReason { get; set; }
    
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

    /// <summary>Período do salário ("Hour", "Month"...); nulo quando a vaga não informa.</summary>
    public string? SalaryPeriod { get; set; }

    // Counters
    public int Views { get; set; }
    public int Applications { get; set; }

    /// <summary>Verdadeiro só quando o candidato autenticado salvou a vaga.</summary>
    public bool IsSaved { get; set; }
    
    // Advertiser
    public Guid? AdvertiserId { get; set; }

    public string PublicationType { get; set; } = "Direct";
    public string ApplicationChannel { get; set; } = "InternalPlatform";
    public string? ApplicationUrl { get; set; }
    public string? ApplicationContact { get; set; }
    public string? CompanyName { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string? CboCode { get; set; }
    public string? AdvertiserName { get; set; }
    public string? AdvertiserLogoUrl { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
