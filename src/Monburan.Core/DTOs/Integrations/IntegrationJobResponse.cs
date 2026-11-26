namespace Monburan.Core.DTOs.Integrations;

public class IntegrationJobResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string ApplyUrl { get; set; } = string.Empty;
}

public class ListIntegrationJobsResponse
{
    public IntegrationJobResponse[] Jobs { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
}
