namespace Monburan.Web.Services;

/// <summary>
/// Rótulos pt-BR para valores de enum que chegam como string nos DTOs
/// (ex.: <c>JobResponse.Status = "Active"</c>). Nenhuma tela deve exibir o valor cru.
/// </summary>
public static class UiLabels
{
    public static string ApplicationStatus(string? status) => status switch
    {
        "Sent" => "Enviada",
        "Viewed" => "Visualizada",
        "Selected" => "Selecionada",
        "Rejected" => "Não selecionada",
        "Archived" => "Arquivada",
        _ => "Em análise"
    };

    /// <summary>Plural para contagens ("3 Não selecionadas"): mesmos termos do singular.</summary>
    public static string ApplicationStatusPlural(string? status) => status switch
    {
        "Sent" => "Enviadas",
        "Viewed" => "Visualizadas",
        "Selected" => "Selecionadas",
        "Rejected" => "Não selecionadas",
        "Archived" => "Arquivadas",
        _ => "Em análise"
    };

    public static string ApplicationStatusBadge(string? status) => status switch
    {
        "Sent" => "bg-primary",
        "Viewed" => "bg-info",
        "Selected" => "bg-success",
        "Rejected" => "bg-danger",
        _ => "bg-secondary"
    };

    public static string JobStatus(string? status) => status switch
    {
        "Active" => "Ativa",
        "Expired" => "Expirada",
        "Hidden" => "Oculta",
        "Rejected" => "Reprovada",
        "Paused" => "Pausada",
        _ => "Indefinida"
    };

    /// <summary>Plural para contagens de vagas: "Rejected" de vaga é reprovação na moderação.</summary>
    public static string JobStatusPlural(string? status) => status switch
    {
        "Active" => "Ativas",
        "Expired" => "Expiradas",
        "Hidden" => "Ocultas",
        "Rejected" => "Reprovadas",
        "Paused" => "Pausadas",
        _ => "Outras"
    };

    public static string JobStatusBadge(string? status) => status switch
    {
        "Active" => "bg-success",
        "Expired" => "bg-secondary",
        "Hidden" => "bg-warning",
        "Rejected" => "bg-danger",
        "Paused" => "bg-info",
        _ => "bg-secondary"
    };

    public static string JobType(string? type) => type switch
    {
        "Remote" => "Remoto",
        "Hybrid" => "Híbrido",
        _ => "Presencial"
    };

    /// <summary>Rótulos pt-BR do tipo de contrato (contrato compartilhado).</summary>
    public static string ContractType(string? contractType) => contractType switch
    {
        "Clt" => "CLT",
        "Pj" => "PJ",
        "Internship" => "Estágio",
        "Apprenticeship" => "Aprendiz",
        "Temporary" => "Temporário",
        "Freelance" => "Freelance",
        _ => string.Empty
    };

    /// <summary>Rótulos pt-BR do período do salário (contrato compartilhado).</summary>
    public static string SalaryPeriod(string? period) => period switch
    {
        "Hour" => "por hora",
        "Day" => "por dia",
        "Week" => "por semana",
        "Month" => "por mês",
        "Year" => "por ano",
        _ => string.Empty
    };

    /// <summary>Nome do plano para a tela: o plano básico se chama "Free" no cadastro, mas a interface é em português.</summary>
    public static string PlanName(string? name) => string.IsNullOrWhiteSpace(name) || name.Trim().Equals("Free", StringComparison.OrdinalIgnoreCase)
        ? "Gratuito"
        : name.Trim();

    public static string SubscriptionStatus(string? status) => status switch
    {
        "Active" => "Ativa",
        "Pending" => "Pendente",
        "Expired" => "Expirada",
        "Cancelled" => "Cancelada",
        _ => "Indefinida"
    };

    public static string SubscriptionStatusBadge(string? status) => status switch
    {
        "Active" => "bg-success",
        "Pending" => "bg-info",
        "Expired" => "bg-warning",
        _ => "bg-secondary"
    };

    public static string AccountStatus(EAccountStatus status) => status switch
    {
        EAccountStatus.Active => "Ativa",
        EAccountStatus.Blocked => "Bloqueada",
        _ => "Cadastro incompleto"
    };

    public static string AccountStatus(EAdvertiserStatus status) => status switch
    {
        EAdvertiserStatus.Active => "Ativa",
        EAdvertiserStatus.Blocked => "Bloqueada",
        _ => "Cadastro incompleto"
    };

    /// <summary>Nível de formação do currículo (<c>EEducationLevel</c>); valor desconhecido aparece como veio.</summary>
    public static string EducationLevel(string? level) => level switch
    {
        "Elementary" => "Ensino fundamental",
        "HighSchool" => "Ensino médio",
        "Technical" => "Técnico",
        "Bachelor" => "Graduação",
        "Postgraduate" => "Pós-graduação",
        "Master" => "Mestrado",
        "Doctorate" => "Doutorado",
        _ => level ?? string.Empty
    };

    /// <summary>Situação do curso (<c>EEducationStatus</c>); valor desconhecido aparece como veio.</summary>
    public static string EducationStatus(string? status) => status switch
    {
        "Ongoing" => "Em andamento",
        "Completed" => "Concluído",
        "Dropped" => "Trancado",
        "Incomplete" => "Incompleto",
        _ => status ?? string.Empty
    };

    /// <summary>Nível do idioma (<c>ELanguageLevel</c>); valor desconhecido aparece como veio.</summary>
    public static string LanguageLevel(string? level) => level switch
    {
        "Basic" => "Básico",
        "Intermediate" => "Intermediário",
        "Advanced" => "Avançado",
        "Fluent" => "Fluente",
        "Native" => "Nativo",
        _ => level ?? string.Empty
    };

    public static string CurrencySymbol(string? currency) => currency switch
    {
        "USD" => "US$",
        "EUR" => "€",
        _ => "R$"
    };

    /// <summary>
    /// Momentos (criação, candidatura, leitura) são gravados em UTC; a exibição usa Brasília.
    /// </summary>
    public static DateTime ToBrasilia(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, BrasiliaTimeZone.Value);
    }

    /// <summary>Momento com hora, em Brasília (ex.: data da candidatura).</summary>
    public static string FormatMoment(DateTime? value) =>
        value.HasValue ? ToBrasilia(value.Value).ToString("dd/MM/yyyy HH:mm") : "—";

    /// <summary>Dia de um momento, em Brasília (ex.: "membro desde").</summary>
    public static string FormatMomentDate(DateTime? value) =>
        value.HasValue ? ToBrasilia(value.Value).ToString("dd/MM/yyyy") : "—";

    /// <summary>
    /// Data de calendário (validade da vaga, nascimento, período de experiência):
    /// exibida como foi digitada, sem conversão de fuso.
    /// </summary>
    public static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("dd/MM/yyyy") : "—";

    private static readonly Lazy<TimeZoneInfo> BrasiliaTimeZone = new(() =>
    {
        // IANA no Linux/containers; o .NET converte para o id do Windows quando preciso
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Imagens sem tzdata: Brasília não tem horário de verão desde 2019
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília");
        }
    });
}
