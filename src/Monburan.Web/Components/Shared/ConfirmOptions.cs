namespace Monburan.Web.Components.Shared;

public sealed record ConfirmOptions
{
    public string Title { get; init; } = "Confirmar ação";

    public string Message { get; init; } = "Deseja continuar?";

    public string ConfirmText { get; init; } = "Confirmar";

    public string CancelText { get; init; } = "Cancelar";

    /// <summary>Ação irreversível: botão vermelho e ícone de alerta.</summary>
    public bool Destructive { get; init; }
}
