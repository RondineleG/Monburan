namespace Monburan.Core.DTOs.Response;

public class PlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int MaxJobs { get; set; }
    public bool HasBoost { get; set; }
    public bool HasHighlight { get; set; }
    public bool HasAnalytics { get; set; }
    public int MaxResumes { get; set; }
    public string Audience { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public bool HasAiAssistant { get; set; }

    /// <summary>O plano não exibe publicidade (regra do servidor: plano pago).</summary>
    public bool IsAdFree { get; set; }
}

public class AvailablePlansResponse
{
    public List<PlanResponse> Plans { get; set; } = [];
    public SubscriptionResponse? CurrentSubscription { get; set; }

    /// <summary>Checkout iniciado e ainda sem confirmação do provedor — só quando não há assinatura ativa.</summary>
    public SubscriptionResponse? PendingSubscription { get; set; }
}
