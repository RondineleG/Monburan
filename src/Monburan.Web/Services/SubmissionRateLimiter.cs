using System.Collections.Concurrent;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Limite de sugestões por remetente numa janela de uma hora. Fica em memória: basta para conter abuso simples, e um
/// reinício só zera o contador.
/// </summary>
public sealed class SubmissionRateLimiter : ISubmissionRateLimiter
{
    private const int MaxPerWindow = 5;
    private const int PruneAbove = 5000;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sent = new(StringComparer.Ordinal);

    public bool TryAcquire(string key, out TimeSpan retryAfter)
    {
        var now = DateTimeOffset.UtcNow;
        var queue = _sent.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Window)
                queue.Dequeue();

            if (queue.Count >= MaxPerWindow)
            {
                retryAfter = queue.Peek() + Window - now;
                return false;
            }

            queue.Enqueue(now);
        }

        if (_sent.Count > PruneAbove)
            Prune(now);

        retryAfter = TimeSpan.Zero;
        return true;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, queue) in _sent)
        {
            lock (queue)
            {
                if (queue.Count == 0 || now - queue.Peek() >= Window)
                    _sent.TryRemove(key, out _);
            }
        }
    }
}
