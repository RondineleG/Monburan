using System.Threading.RateLimiting;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Limite de sugestões por remetente numa janela de uma hora. Fica em memória: basta para conter abuso simples, e um
/// reinício só zera o contador.
/// </summary>
/// <remarks>
/// Usa o <see cref="PartitionedRateLimiter{TResource}"/> do framework (uma janela deslizante por remetente): as chaves
/// ociosas são removidas por um timer interno, fora da requisição, e a contagem é atômica. A versão anterior percorria o
/// dicionário inteiro a cada chamada acima de 5000 chaves (O(n) por requisição) e podia perder contagem ao remover uma
/// fila que outra thread ainda usava.
/// </remarks>
public sealed class SubmissionRateLimiter : ISubmissionRateLimiter, IDisposable
{
    public const int DefaultMaxPerWindow = 5;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(1);

    private readonly PartitionedRateLimiter<string> _limiter;
    private readonly TimeSpan _window;

    public SubmissionRateLimiter() : this(DefaultMaxPerWindow, DefaultWindow)
    {
    }

    public SubmissionRateLimiter(int maxPerWindow, TimeSpan window)
    {
        _window = window;
        _limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = maxPerWindow,
                Window = window,
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public bool TryAcquire(string key, out TimeSpan retryAfter)
    {
        using var lease = _limiter.AttemptAcquire(key);
        if (lease.IsAcquired)
        {
            retryAfter = TimeSpan.Zero;
            return true;
        }

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? delay : _window;
        return false;
    }

    /// <summary>Consulta sem consumir permissão: a tela recusa antes de transferir as imagens.</summary>
    public bool IsExhausted(string key) => _limiter.GetStatistics(key)?.CurrentAvailablePermits < 1;

    public void Dispose() => _limiter.Dispose();
}
