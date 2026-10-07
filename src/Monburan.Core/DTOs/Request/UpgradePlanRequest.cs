using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class UpgradePlanRequest
{
    [Required]
    public Guid AdvertiserId { get; set; }

    [Required]
    public Guid NewPlanId { get; set; }

    [Required, MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;
}
