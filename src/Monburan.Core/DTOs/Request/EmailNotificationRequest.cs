namespace Monburan.Core.DTOs.Request;

public class EmailNotificationRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public Dictionary<string, object?> TemplateData { get; set; } = [];
}
