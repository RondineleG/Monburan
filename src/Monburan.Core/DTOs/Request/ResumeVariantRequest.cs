namespace Monburan.Core.DTOs.Request;

public class ResumeVariantCreateRequest
{
    /// <summary>Tag BCP-47 (ex.: "es-ES").</summary>
    public string Locale { get; set; } = string.Empty;
}

/// <summary>Substituições de texto localizado — chaves conforme documentado em ResumeVariant.</summary>
public class ResumeVariantUpdateRequest
{
    public Dictionary<string, string> Overrides { get; set; } = [];
}

/// <summary>Aceitar (true) aplica as sugestões de IA pendentes; rejeitar (false) descarta.</summary>
public class ResumeVariantResolveSuggestionsRequest
{
    public bool Accept { get; set; }
}
