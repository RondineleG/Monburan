namespace Monburan.Core.DTOs.Response;

public class ListCandidatesResponse
{
    public IEnumerable<CandidateResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Size);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
