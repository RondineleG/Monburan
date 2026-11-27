using Monburan.Core.Entities;
using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>
/// Quem vê publicidade. Fica no servidor: a escolha do anúncio e a tela de planos usam a mesma regra.
/// </summary>
public static class AdvertisingRules
{
    /// <summary>
    /// Um plano pago (preço maior que zero) não exibe publicidade. O nome do plano nunca entra na conta.
    /// </summary>
    public static bool IsAdFreePlan(decimal price) => price > 0;

    /// <summary>
    /// A conta tem direito à experiência sem publicidade: assinatura ativa, dentro da vigência, de um plano pago.
    /// Pendente, cancelada, expirada ou sem plano carregado conta como sem o direito.
    /// </summary>
    public static bool IsAdFree(Subscription? subscription, DateTime nowUtc) =>
        subscription is { Status: ESubscriptionStatus.Active, IsDeleted: false, Plan: { } plan }
        && (subscription.EndDate is null || subscription.EndDate > nowUtc)
        && IsAdFreePlan(plan.Price);
}
