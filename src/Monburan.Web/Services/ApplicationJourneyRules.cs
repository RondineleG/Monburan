namespace Monburan.Web.Services;

/// <summary>Filtro de situação da listagem "Minhas candidaturas".</summary>
public enum ApplicationJourneyFilter
{
    All,
    InProgress,
    Selected,
    Closed
}

/// <summary>Regras de agrupamento das candidaturas do candidato. Regra pura, testada em unitário.</summary>
public static class ApplicationJourneyRules
{
    /// <summary>"Em andamento": enviada ou já vista pela empresa.</summary>
    public static bool IsInProgress(string? status) => status is "Sent" or "Viewed";

    public static bool IsClosed(string? status) => status is "Rejected" or "Archived";

    public static bool Matches(string? status, ApplicationJourneyFilter filter) => filter switch
    {
        ApplicationJourneyFilter.InProgress => IsInProgress(status),
        ApplicationJourneyFilter.Selected => status == "Selected",
        ApplicationJourneyFilter.Closed => IsClosed(status),
        _ => true
    };

    /// <summary>Valor da query string para o filtro de situação (padrão: todas).</summary>
    public static ApplicationJourneyFilter Parse(string? value) => value switch
    {
        "andamento" => ApplicationJourneyFilter.InProgress,
        "selecionadas" => ApplicationJourneyFilter.Selected,
        "encerradas" => ApplicationJourneyFilter.Closed,
        _ => ApplicationJourneyFilter.All
    };
}
