namespace Monburan.Core.DTOs.Request;

public class ListAdvertisersRequest
{
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
}
