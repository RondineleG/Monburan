using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public sealed class CreateSubscriptionCheckoutRequest
{
    [Required]
    public Guid PlanId { get; set; }
}
