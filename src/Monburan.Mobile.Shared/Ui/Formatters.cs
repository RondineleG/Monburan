using System.Globalization;

using Monburan.Core.DTOs.Response;

namespace Monburan.Mobile.Shared.Ui;

/// <summary>
/// Formatação de exibição do app (mesmas regras do site): salário respeitando
/// <see cref="JobResponse.SalaryDisplay"/>, moeda da vaga, horários no fuso do aparelho
/// e rótulos pt-BR dos status que chegam como string.
/// </summary>
public static class Formatters
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Faixa salarial ou <c>null</c> quando a empresa ocultou/não informou.</summary>
    public static string? Salary(JobResponse job)
    {
        if (!job.SalaryDisplay || (!job.SalaryMin.HasValue && !job.SalaryMax.HasValue))
            return null;

        var symbol = CurrencySymbol(job.SalaryCurrency);
        var min = job.SalaryMin?.ToString("N0", PtBr);
        var max = job.SalaryMax?.ToString("N0", PtBr);
        return (min, max) switch
        {
            (not null, not null) => $"{symbol} {min} – {max}",
            (not null, null) => $"a partir de {symbol} {min}",
            _ => $"até {symbol} {max}"
        };
    }

    public static string CurrencySymbol(string? currency) => currency switch
    {
        "USD" => "US$",
        "EUR" => "€",
        _ => "R$"
    };

    public static string JobType(string? type) => type switch
    {
        "Remote" => "Remoto",
        "Hybrid" => "Híbrido",
        _ => "Presencial"
    };

    /// <summary>
    /// Modalidade da vaga pelo campo <see cref="JobResponse.Type"/> (Remote, Hybrid, OnSite). <c>IsRemote</c> só vale
    /// quando a vaga não traz o tipo: vaga híbrida também chega com <c>IsRemote</c> e não pode aparecer como remota.
    /// </summary>
    public static string JobMode(JobResponse job) =>
        string.IsNullOrWhiteSpace(job.Type) ? (job.IsRemote ? "Remote" : "OnSite") : job.Type;

    /// <summary>Nome do ícone da modalidade (mesmos da web: casa, setas, prédio).</summary>
    public static string JobModeIcon(string? type) => type switch
    {
        "Remote" => "home",
        "Hybrid" => "shuffle",
        _ => "building"
    };

    public static string ApplicationStatus(string? status) => status switch
    {
        "Sent" => "Enviada",
        "Viewed" => "Visualizada",
        "Selected" => "Selecionada",
        "Rejected" => "Não selecionada",
        "Archived" => "Arquivada",
        _ => "Em análise"
    };

    /// <summary>
    /// Tom do selo de situação (classe <c>badge--{tom}</c>), igual a <c>UiLabels.ApplicationStatusTone</c> da web:
    /// a cor nunca é o único sinal, o rótulo vai junto.
    /// </summary>
    public static string ApplicationStatusTone(string? status) => status switch
    {
        "Viewed" => "info",
        "Selected" => "ok",
        "Rejected" => "er",
        "Sent" or "Archived" => "neutral",
        _ => "brand"
    };

    /// <summary>Momento (UTC no servidor) no fuso do aparelho, com hora.</summary>
    public static string Moment(DateTime value) => ToLocal(value).ToString("dd/MM/yyyy HH:mm", PtBr);

    /// <summary>Dia de um momento, no fuso do aparelho.</summary>
    public static string MomentDate(DateTime value) => ToLocal(value).ToString("dd/MM/yyyy", PtBr);

    /// <summary>Data de calendário (validade, nascimento): exibida como gravada.</summary>
    public static string CalendarDate(DateTime value) => value.ToString("dd/MM/yyyy", PtBr);

    private static DateTime ToLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
        _ => value
    };
}
