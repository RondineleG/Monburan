namespace Monburan.Core.DTOs.Response;

public class SubscriptionResponse
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Price { get; set; }
}
