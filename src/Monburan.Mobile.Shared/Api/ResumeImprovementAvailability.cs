namespace Monburan.Mobile.Shared.Api;

/// <summary>
/// Resposta de <c>GET resumes/improvements/availability</c> (objeto anônimo no servidor, sem DTO no Core).
/// </summary>
public sealed class ResumeImprovementAvailability
{
    /// <summary>IA configurada e candidato com plano que inclui o assistente.</summary>
    public bool Enabled { get; set; }

    /// <summary>Provedor que recebe o conteúdo (transparência no pedido de autorização).</summary>
    public string? Provider { get; set; }

    /// <summary>Candidato com assinatura ativa de plano com assistente de IA.</summary>
    public bool Premium { get; set; }
}
