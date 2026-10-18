using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>
/// Registro de uma ação de administração (quem, quando, o quê e sobre quem). Só se acrescenta: nunca é alterado nem
/// excluído pela aplicação. Não guarda segredo nenhum, só nomes, motivos e identificadores.
/// </summary>
public class AdminAuditEntry : BaseEntity
{
    /// <summary>Chave estável da ação (ver <c>AdminAuditActions</c>), ex.: <c>account.deactivated</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Id da conta de quem agiu.</summary>
    public string ActorId { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    /// <summary>Tipo do alvo: <c>account</c>, <c>job</c>, <c>aiProvider</c>.</summary>
    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    /// <summary>Texto legível do alvo na hora da ação (e-mail da conta, título da vaga, nome do provedor).</summary>
    public string TargetLabel { get; set; } = string.Empty;

    /// <summary>Motivo ou observação (sem segredos).</summary>
    public string? Details { get; set; }
}
