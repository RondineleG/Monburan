namespace Monburan.Core.DTOs.Response;

public class ListJobsResponse
{
    public IEnumerable<JobResponse> Jobs { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Size);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
