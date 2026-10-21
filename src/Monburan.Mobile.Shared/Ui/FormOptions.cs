namespace Monburan.Mobile.Shared.Ui;

/// <summary>Opções fixas dos formulários do app (mesmas da web).</summary>
public static class FormOptions
{
    public static IReadOnlyList<string> States { get; } =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    /// <summary>Modalidades aceitas pela API (Mode/Type), na ordem da web.</summary>
    public static IReadOnlyList<(string Value, string Label)> Modes { get; } =
    [
        ("OnSite", "Presencial"),
        ("Remote", "Remoto"),
        ("Hybrid", "Híbrido")
    ];
}
