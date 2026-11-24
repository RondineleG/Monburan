using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

/// <summary>Item de idioma do candidato (tag BCP-47 + nível autodeclarado + evidência declarada).</summary>
public class CandidateLanguageItem
{
    public string LanguageTag { get; set; } = string.Empty;

    public ELanguageLevel Level { get; set; } = ELanguageLevel.Basic;

    public ELanguageEvidence Evidence { get; set; } = ELanguageEvidence.SelfDeclared;
}
