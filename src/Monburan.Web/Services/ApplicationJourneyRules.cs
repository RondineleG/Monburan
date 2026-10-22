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

    /// <summary>Próximo passo amigável por situação, exibido em cada candidatura da lista.</summary>
    public static string NextStep(string? status) => status switch
    {
        "Sent" => "Enviada — aguardando a empresa visualizar.",
        "Viewed" => "A empresa já visualizou sua candidatura.",
        "Selected" => "Você foi selecionado(a)! Acompanhe os próximos contatos da empresa.",
        "Rejected" => "Processo encerrado para esta vaga.",
        "Archived" => "Candidatura arquivada pela empresa.",
        _ => "Acompanhe as atualizações por aqui."
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
