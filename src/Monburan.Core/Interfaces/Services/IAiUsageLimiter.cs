namespace Monburan.Core.Interfaces.Services;

/// <summary>Limite de uso dos recursos de IA por pessoa: cada chamada ao provedor tem custo.</summary>
public interface IAiUsageLimiter
{
    /// <summary>
    /// Reserva uma chamada para a chave. Devolve <c>false</c>, com o tempo até liberar a próxima, quando a janela
    /// já foi usada por inteiro.
    /// </summary>
    bool TryAcquire(string key, out TimeSpan retryAfter);
}
