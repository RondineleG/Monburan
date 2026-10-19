namespace Monburan.Core.DTOs.Request;

public class JobAlertRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Keywords { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    /// <summary>"OnSite", "Remote" ou "Hybrid" (mesmos valores de <see cref="Entities.JobLocation.Type"/>).</summary>
    public string? Mode { get; set; }

    public string? Category { get; set; }

    public decimal? SalaryMin { get; set; }

    /// <summary>Padrão verdadeiro na criação; nulo mantém o valor atual na edição.</summary>
    public bool? IsActive { get; set; }
}
