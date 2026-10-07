namespace Monburan.Core.DTOs.Request;

public class ListJobsRequest
{
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public bool? IsRemote { get; set; }
    public string? Type { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? Category { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public Guid? AdvertiserId { get; set; }
}
