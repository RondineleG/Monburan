using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Models;

/// <summary>
/// Rascunho de sugestões de IA salvo no navegador (armazenamento local criptografado,
/// escopo por candidato + currículo). Guarda o currículo original, o resultado gerado
/// e as edições/seleções da pessoa — nunca vai para o banco nem para o provedor de novo.
/// </summary>
public sealed class ResumeImprovementDraft
{
    public Guid CandidateId { get; set; }

    public Guid ResumeId { get; set; }

    /// <summary>Como o currículo estava quando as sugestões foram geradas (base de comparação).</summary>
    public ResumeResponse? ResumeSnapshot { get; set; }

    /// <summary>O que a IA sugeriu na geração que originou este rascunho.</summary>
    public ResumeImprovementResponse? Result { get; set; }

    public bool SummarySelected { get; set; }

    public string? EditedSummary { get; set; }

    public List<ResumeImprovementDraftExperience> Experiences { get; set; } = [];

    public List<ResumeImprovementDraftSkill> Skills { get; set; } = [];

    public DateTime GeneratedAtUtc { get; set; }

    public DateTime SavedAtUtc { get; set; }
}

/// <summary>Uma sugestão de experiência já resolvida contra o currículo original.</summary>
public sealed class ResumeImprovementDraftExperience
{
    public string Company { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    /// <summary>Índice da experiência original no currículo; nulo quando não deu para atribuir (sem par único).</summary>
    public int? MatchedIndex { get; set; }

    public bool Selected { get; set; }

    /// <summary>Descrição completa resultante (original + sugestões), editável pela pessoa.</summary>
    public string EditedDescription { get; set; } = string.Empty;
}

public sealed class ResumeImprovementDraftSkill
{
    public string Skill { get; set; } = string.Empty;

    public bool Selected { get; set; }
}
