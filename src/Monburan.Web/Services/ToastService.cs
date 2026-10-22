namespace Monburan.Web.Services;

public enum ToastLevel
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record ToastMessage(Guid Id, ToastLevel Level, string Message, string? Title);

/// <summary>
/// Feedback não bloqueante para ações do usuário (salvou, excluiu, falhou...).
/// Escopo por circuito: a página e o <c>ToastHost</c> do layout compartilham a instância,
/// então um toast disparado antes de navegar continua visível na página seguinte.
/// </summary>
public interface IToastService
{
    event Action? Changed;

    IReadOnlyList<ToastMessage> Toasts { get; }

    void ShowSuccess(string message, string? title = null);

    void ShowError(string message, string? title = null);

    void ShowWarning(string message, string? title = null);

    void ShowInfo(string message, string? title = null);

    void Dismiss(Guid id);

    /// <summary>Segura o aviso na tela enquanto o ponteiro ou o foco estão sobre ele (WCAG 2.2.1).</summary>
    void Pause(Guid id);

    /// <summary>Retoma a contagem com o tempo inteiro do nível do aviso.</summary>
    void Resume(Guid id);
}

public sealed class ToastService : IToastService, IDisposable
{
    /// <summary>Toasts empilhados ao mesmo tempo; o mais antigo sai quando passa disso.</summary>
    public const int MaxVisible = 4;

    private readonly Lock _gate = new();
    private readonly List<ToastMessage> _toasts = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _timers = [];

    public event Action? Changed;

    /// <summary>Tempo de exibição de sucesso/aviso/informação.</summary>
    public TimeSpan DefaultDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Erros ficam até a pessoa fechar (<c>null</c>): ela precisa ler o que fazer, no próprio ritmo.
    /// </summary>
    public TimeSpan? ErrorDuration { get; init; }

    public IReadOnlyList<ToastMessage> Toasts
    {
        get
        {
            lock (_gate)
                return _toasts.ToArray();
        }
    }

    public void ShowSuccess(string message, string? title = null) => Show(ToastLevel.Success, message, title);

    public void ShowError(string message, string? title = null) => Show(ToastLevel.Error, message, title);

    public void ShowWarning(string message, string? title = null) => Show(ToastLevel.Warning, message, title);

    public void ShowInfo(string message, string? title = null) => Show(ToastLevel.Info, message, title);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _toasts.RemoveAll(t => t.Id == id) > 0;
            CancelTimer(id);
        }

        if (removed)
            Changed?.Invoke();
    }

    public void Pause(Guid id)
    {
        lock (_gate)
            CancelTimer(id);
    }

    public void Resume(Guid id)
    {
        lock (_gate)
        {
            var toast = _toasts.Find(t => t.Id == id);
            if (toast is null || _timers.ContainsKey(id))
                return;

            StartTimer(toast);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Cancel();
                timer.Dispose();
            }

            _timers.Clear();
            _toasts.Clear();
        }
    }

    private void Show(ToastLevel level, string message, string? title)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var toast = new ToastMessage(Guid.NewGuid(), level, message.Trim(), title);

        lock (_gate)
        {
            _toasts.Add(toast);
            StartTimer(toast);

            while (_toasts.Count > MaxVisible)
            {
                var oldest = _toasts[0];
                _toasts.RemoveAt(0);
                CancelTimer(oldest.Id);
            }
        }

        Changed?.Invoke();
    }

    // Chamado sob _gate
    private void StartTimer(ToastMessage toast)
    {
        var duration = toast.Level == ToastLevel.Error ? ErrorDuration : DefaultDuration;
        if (duration is null)
            return;

        var timer = new CancellationTokenSource();
        // Token lido antes de publicar o timer: outro Show pode descartá-lo ao remover o mais antigo
        var token = timer.Token;
        _timers[toast.Id] = timer;
        _ = DismissLaterAsync(toast.Id, duration.Value, token);
    }

    private async Task DismissLaterAsync(Guid id, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Fechado manualmente, substituído ou serviço descartado
            return;
        }

        Dismiss(id);
    }

    private void CancelTimer(Guid id)
    {
        if (_timers.Remove(id, out var timer))
        {
            timer.Cancel();
            timer.Dispose();
        }
    }
}
