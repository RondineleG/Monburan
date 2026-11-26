using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class SubscriptionStatusRequest
{
    [Required]
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Anunciante autenticado. Preenchido pelo servidor a partir da claim — nunca do corpo da
    /// requisição. O status só é devolvido se a assinatura for deste anunciante.
    /// </summary>
    public Guid? AdvertiserId { get; set; }

    /// <summary>
    /// Candidato autenticado. Preenchido pelo servidor a partir da claim — nunca do corpo da
    /// requisição. O status só é devolvido se a assinatura for deste candidato.
    /// </summary>
    public Guid? CandidateId { get; set; }
}
