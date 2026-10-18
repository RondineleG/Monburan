using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Limite de usos de IA por pessoa numa janela deslizante de uma hora, em memória. Num único servidor basta; com
/// várias instâncias cada uma conta por conta própria (o limite efetivo vira o múltiplo).
/// </summary>
public sealed class MemoryAiUsageLimiter : IAiUsageLimiter
{
    private const int PruneAbove = 5000;

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _usage = new(StringComparer.Ordinal);
    private readonly IOptionsMonitor<AiOptions> _options;
    private readonly TimeProvider _time;

    public MemoryAiUsageLimiter(IOptionsMonitor<AiOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public bool TryAcquire(string key, out TimeSpan retryAfter)
    {
        var limit = Math.Max(1, _options.CurrentValue.MaxRequestsPerHour);
        var now = _time.GetUtcNow();

        if (_usage.Count > PruneAbove)
            Prune(now);

        var uses = _usage.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (uses)
        {
            while (uses.Count > 0 && now - uses.Peek() >= Window)
                uses.Dequeue();

            if (uses.Count >= limit)
            {
                retryAfter = uses.Peek() + Window - now;
                return false;
            }

            uses.Enqueue(now);
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    // Remove quem não usou na última janela, para a tabela não crescer sem fim
    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, uses) in _usage)
        {
            bool stale;
            lock (uses)
            {
                stale = uses.Count == 0 || now - uses.Last() >= Window;
            }

            if (stale)
                _usage.TryRemove(key, out _);
        }
    }
}
