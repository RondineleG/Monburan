using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>
/// Sugestão anônima de vaga, separada da vaga publicada. Fica pendente até um moderador decidir; a evidência é
/// privada e nunca vai para a página pública.
/// </summary>
public class JobSubmission : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Description { get; set; }

    /// <summary>Endereço da publicação original, quando a pessoa tiver.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Descrição de onde a vaga foi encontrada, quando não houver endereço.</summary>
    public string? SourceDescription { get; set; }

    /// <summary>Contato opcional para pedidos de esclarecimento. Não é obrigatório para enviar.</summary>
    public string? ContactEmail { get; set; }

    public ESubmissionStatus Status { get; set; } = ESubmissionStatus.Pending;

    public string? ModerationNote { get; set; }

    public string? ModeratedBy { get; set; }

    public DateTime? ModeratedAt { get; set; }

    /// <summary>Vaga publicada a partir desta sugestão (preenchido só na aprovação).</summary>
    public Id? ApprovedJobId { get; set; }

    /// <summary>Vaga já publicada que esta sugestão repete (decisão de duplicidade).</summary>
    public Id? DuplicateOfJobId { get; set; }

    /// <summary>Sugestão anterior que esta repete (decisão de duplicidade).</summary>
    public Id? DuplicateOfSubmissionId { get; set; }

    /// <summary>Hash do IP de quem enviou, para proteção contra abuso sem guardar o endereço.</summary>
    public string? SubmitterKeyHash { get; set; }

    /// <summary>Rascunho da vaga extraído pela IA (JSON dos campos). Sugestão para o moderador, nunca publicada sozinha.</summary>
    public string? AiDraftJson { get; set; }

    /// <summary>Impressão digital das evidências que geraram o rascunho; evita reprocessar as mesmas imagens.</summary>
    public string? AiDraftEvidenceHash { get; set; }

    public string? AiDraftProvider { get; set; }

    public DateTime? AiDraftAt { get; set; }

    public virtual ICollection<JobSubmissionEvidence> Evidences { get; set; } = [];

    public virtual ICollection<JobSubmissionHistory> History { get; set; } = [];
}
