using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Subscription : BaseEntity
{
    public Subscription()
    {
        InitializeNewEntity();
    }

    /// <summary>Dono empresa da assinatura. Exatamente um de <c>AdvertiserId</c>/<c>CandidateId</c> é preenchido.</summary>
    public Id? AdvertiserId { get; set; }

    public virtual Advertiser? Advertiser { get; set; }

    /// <summary>Dono candidato da assinatura (planos com <see cref="EPlanAudience.Candidate"/>).</summary>
    public Id? CandidateId { get; set; }

    public virtual Candidate? Candidate { get; set; }

    [Required]
    public Id PlanId { get; set; } = Id.Empty;

    public virtual SubscriptionPlan? Plan { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    [Required]
    public ESubscriptionStatus Status { get; set; } = ESubscriptionStatus.Pending;

    /// <summary>
    /// ID do gateway de pagamento (referência externa).
    /// </summary>
    [MaxLength(100)]
    public string? PaymentId { get; set; }

    /// <summary>
    /// Motivo do cancelamento (quando aplicável).
    /// </summary>
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
}
