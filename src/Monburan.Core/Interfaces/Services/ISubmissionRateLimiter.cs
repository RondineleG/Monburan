namespace Monburan.Core.Interfaces.Services;

/// <summary>Limite de envios anônimos por chave (hash do remetente), para conter abuso sem exigir cadastro.</summary>
public interface ISubmissionRateLimiter
{
    bool TryAcquire(string key, out TimeSpan retryAfter);
}
