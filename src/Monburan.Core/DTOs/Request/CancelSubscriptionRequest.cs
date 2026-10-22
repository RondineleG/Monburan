using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class CancelSubscriptionRequest
{
    [Required]
    public Guid SubscriptionId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Anunciante autenticado. Preenchido pelo servidor a partir da claim — nunca do corpo da
    /// requisição. A assinatura só é cancelada se for deste anunciante.
    /// </summary>
    public Guid? AdvertiserId { get; set; }
}
