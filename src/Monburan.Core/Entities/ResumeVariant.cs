using Monburan.Core.Entities.Base;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>
/// Variante localizada de um currículo (ex.: es-ES). Guarda APENAS substituições de texto —
/// a estrutura (experiências, formação, habilidades) vem sempre do currículo base na hora de
/// renderizar, sem duplicar dados. Chaves de override:
/// <c>title</c>, <c>summary</c>, <c>experience:{índice}:position|company|description</c>,
/// <c>education:{índice}:institution|course</c>, <c>skills:{índice}</c>, <c>languages:{índice}</c>.
/// O índice segue a ordenação do currículo base.
/// </summary>
public sealed class ResumeVariant : BaseEntity
{
    public Id ResumeId { get; set; } = Id.Empty;

    /// <summary>Desnormalizado para checagem de posse sem carregar o currículo.</summary>
    public Id CandidateId { get; set; } = Id.Empty;

    /// <summary>Tag BCP-47 da localização (ex.: "es-ES"). Única por currículo.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>JSON {"chave":"texto localizado"} com as substituições confirmadas.</summary>
    public string OverridesJson { get; set; } = "{}";

    /// <summary>Sugestões geradas por IA aguardando revisão explícita — nunca aplicadas sozinhas.</summary>
    public string? PendingSuggestionsJson { get; set; }

    public bool IsActive { get; set; } = true;
}
