using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class SubscriptionPlan : BaseEntity
{
    public SubscriptionPlan()
    {
        InitializeNewEntity();
    }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>
    /// Número máximo de vagas ativas permitidas. -1 = ilimitado.
    /// </summary>
    public int MaxJobs { get; set; }

    /// <summary>
    /// Indica se o plano inclui boost de vagas.
    /// </summary>
    public bool HasBoost { get; set; }

    /// <summary>
    /// Indica se o plano inclui destaque de vagas.
    /// </summary>
    public bool HasHighlight { get; set; }

    /// <summary>
    /// Indica se o plano inclui analytics.
    /// </summary>
    public bool HasAnalytics { get; set; }

    /// <summary>
    /// Número máximo de currículos que o anunciante pode criar.
    /// </summary>
    public int MaxResumes { get; set; }

    /// <summary>
    /// Para qual perfil o plano vale: empresa (<see cref="EPlanAudience.Advertiser"/>) ou
    /// candidato (<see cref="EPlanAudience.Candidate"/>). Um plano nunca vale para os dois.
    /// </summary>
    public EPlanAudience Audience { get; set; } = EPlanAudience.Advertiser;

    /// <summary>
    /// Ciclo de cobrança: mensal e anual geram <c>Subscription.EndDate</c> (+1 mês, +12 meses);
    /// <c>None</c> é o plano sem vigência (Free), que não expira.
    /// </summary>
    public ESubscriptionPeriod Period { get; set; } = ESubscriptionPeriod.Monthly;

    /// <summary>
    /// Inclui o assistente de IA do currículo (sugestões de melhoria). Vale para planos de candidato.
    /// </summary>
    public bool HasAiAssistant { get; set; }

    /// <summary>
    /// Indica se o plano está ativo para assinatura.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
