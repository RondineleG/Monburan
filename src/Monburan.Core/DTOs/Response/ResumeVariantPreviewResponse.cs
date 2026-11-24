namespace Monburan.Core.DTOs.Response;

/// <summary>Preview da variante já com o merge (base + overrides localizados), pronto para renderizar/exportar.</summary>
public class ResumeVariantPreviewResponse
{
    public Guid VariantId { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CandidateName { get; set; }
    public List<VariantExperienceItem> Experience { get; set; } = [];
    public List<VariantEducationItem> Education { get; set; } = [];
    public List<string> Skills { get; set; } = [];
    public List<VariantLanguageItem> Languages { get; set; } = [];
    /// <summary>Sugestões de IA pendentes de revisão (só existem até aceitar/recusar).</summary>
    public Dictionary<string, string>? PendingSuggestions { get; set; }
}

public class VariantExperienceItem
{
    public string Position { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class VariantEducationItem
{
    public string Course { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class VariantLanguageItem
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}
