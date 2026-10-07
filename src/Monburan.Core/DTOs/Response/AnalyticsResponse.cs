namespace Monburan.Core.DTOs.Response;

public class DashboardAnalyticsResponse
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int TotalApplications { get; set; }
    public int TotalCandidates { get; set; }
    public int TotalAdvertisers { get; set; }

    public Dictionary<string, int> JobsByStatus { get; set; } = new();
    public Dictionary<string, int> ApplicationsByStatus { get; set; } = new();

    public List<JobResponse> RecentJobs { get; set; } = new();
    public List<ApplicationResponse> RecentApplications { get; set; } = new();
}
