namespace Monburan.Mobile.Shared.Ui;

/// <summary>
/// Avisa o layout de que o número de avisos não lidos mudou (por exemplo, depois de marcar como lido), para ele reler na hora
/// em vez de esperar o intervalo mínimo entre leituras.
/// </summary>
public sealed class NotificationBadgeState
{
    public event Action? Invalidated;

    public void Invalidate() => Invalidated?.Invoke();
}
