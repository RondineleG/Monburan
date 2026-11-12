namespace Monburan.Core.DTOs.Response;

/// <summary>Status pontual de uma assinatura — usado pela tela de retorno do checkout para acompanhar a confirmação.</summary>
public class SubscriptionStatusResponse
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Period { get; set; } = string.Empty;
}
