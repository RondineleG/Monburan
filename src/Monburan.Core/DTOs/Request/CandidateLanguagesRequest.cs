namespace Monburan.Core.DTOs.Request;

/// <summary>Conjunto completo de idiomas do candidato — PUT substitui a coleção inteira.</summary>
public class CandidateLanguagesRequest
{
    public List<CandidateLanguageItem> Languages { get; set; } = [];
}
