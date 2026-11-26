namespace Monburan.Core.Interfaces.Services;

/// <summary>Cancelamento remoto de uma cobrança recorrente antes de encerrar a assinatura local.</summary>
public interface IRecurringPaymentGateway
{
    Task<bool> CancelAsync(string? paymentId, CancellationToken cancellationToken = default);
}
