namespace Monburan.Core.DTOs.Response;

/// <summary>Variante localizada do currículo — Overrides contém só os textos que divergem do base.</summary>
public class ResumeVariantResponse
{
    public Guid Id { get; set; }
    public Guid ResumeId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public Dictionary<string, string> Overrides { get; set; } = [];
    public bool HasPendingSuggestions { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
