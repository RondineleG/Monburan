namespace Monburan.Core.Interfaces.Services;

/// <summary>Limite de envios anônimos por chave (hash do remetente), para conter abuso sem exigir cadastro.</summary>
public interface ISubmissionRateLimiter
{
    /// <summary>Consome uma permissão da chave; quando o limite está esgotado, informa em quanto tempo libera.</summary>
    bool TryAcquire(string key, out TimeSpan retryAfter);

    /// <summary>
    /// Diz se o limite da chave já está esgotado, sem consumir permissão. Permite recusar antes de ler anexos grandes.
    /// </summary>
    bool IsExhausted(string key);
}
