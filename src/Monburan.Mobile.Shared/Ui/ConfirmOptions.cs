namespace Monburan.Mobile.Shared.Ui;

public sealed record ConfirmOptions
{
    public string Title { get; init; } = "Confirmar ação";

    public string Message { get; init; } = "Deseja continuar?";

    public string ConfirmText { get; init; } = "Confirmar";

    public string CancelText { get; init; } = "Cancelar";

    /// <summary>Ação irreversível: botão vermelho.</summary>
    public bool Destructive { get; init; }
}
