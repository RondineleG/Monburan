namespace Monburan.Core.DTOs.Response;

public class PagedResponse<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Size);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
