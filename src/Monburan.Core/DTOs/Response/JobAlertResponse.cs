namespace Monburan.Core.DTOs.Response;

public class JobAlertResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Keywords { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Mode { get; set; }

    public string? Category { get; set; }

    public decimal? SalaryMin { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMatchedAt { get; set; }
}
