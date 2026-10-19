namespace Monburan.Core.DTOs.Response;

public class ApplicationHistoryItemResponse
{
    public string Status { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public string? Note { get; set; }

    public string? Responsible { get; set; }
}
