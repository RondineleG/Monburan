using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Idioma do candidato com nível autodeclarado — perfil internacional (MI-01).</summary>
public sealed class CandidateLanguage : BaseEntity
{
    public Id CandidateId { get; set; } = Id.Empty;

    /// <summary>Tag BCP-47 normalizada (ex.: "es-ES", "en-US", "pt-BR").</summary>
    public string LanguageTag { get; set; } = string.Empty;

    public ELanguageLevel Level { get; set; }

    public ELanguageEvidence Evidence { get; set; }
}
