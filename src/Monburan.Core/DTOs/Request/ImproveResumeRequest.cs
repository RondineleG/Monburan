namespace Monburan.Core.DTOs.Request;

public class ImproveResumeRequest
{
    /// <summary>Vem da rota; o corpo da requisição não escolhe o currículo.</summary>
    public Guid ResumeId { get; set; }

    /// <summary>Sempre da identidade autenticada, nunca do cliente.</summary>
    public Guid CandidateId { get; set; }

    /// <summary>
    /// Autorização expressa para enviar o conteúdo profissional do currículo a um provedor de IA externo. É
    /// pedida a cada uso, em qualquer cliente (site, app ou API).
    /// </summary>
    public bool AcceptsExternalProcessing { get; set; }
}
