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

    /// <summary>Mesma regra da web (UiLabels.CurrencySymbol): moeda desconhecida aparece pelo código, nunca como "R$".</summary>
    public static string CurrencySymbol(string? currency) => currency?.Trim().ToUpperInvariant() switch
    {
        null or "" or "BRL" => "R$",
        "USD" => "US$",
        "EUR" => "€",
        "GBP" => "£",
        var code => code
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

    /// <summary>Próximo passo amigável por situação, igual a <c>ApplicationJourneyRules.NextStep</c> da web.</summary>
    public static string ApplicationNextStep(string? status) => status switch
    {
        "Sent" => "Enviada — aguardando a empresa visualizar.",
        "Viewed" => "A empresa já visualizou sua candidatura.",
        "Selected" => "Você foi selecionado(a)! Acompanhe os próximos contatos da empresa.",
        "Rejected" => "Processo encerrado para esta vaga.",
        "Archived" => "Candidatura arquivada pela empresa.",
        _ => "Acompanhe as atualizações por aqui."
    };

    /// <summary>"Cidade/UF", só a cidade, só a UF ou <c>null</c>.</summary>
    public static string? Location(string? city, string? state) =>
        (string.IsNullOrWhiteSpace(city), string.IsNullOrWhiteSpace(state)) switch
        {
            (false, false) => $"{city}/{state}",
            (false, true) => city,
            (true, false) => state,
            _ => null
        };

    /// <summary>Nome do país pelo código ISO (mesma lista da web, UiLabels.CountryName).</summary>
    public static string CountryName(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        null or "" or "BR" => "Brasil",
        "PT" => "Portugal",
        "US" => "Estados Unidos",
        "CA" => "Canadá",
        "GB" or "UK" => "Reino Unido",
        "IE" => "Irlanda",
        "DE" => "Alemanha",
        "ES" => "Espanha",
        "FR" => "França",
        "IT" => "Itália",
        "NL" => "Países Baixos",
        "AR" => "Argentina",
        "CL" => "Chile",
        "UY" => "Uruguai",
        "MX" => "México",
        "AU" => "Austrália",
        "JP" => "Japão",
        var other => other
    };

    public static bool IsAbroad(string? countryCode) =>
        !string.IsNullOrWhiteSpace(countryCode) && !string.Equals(countryCode.Trim(), "BR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Local da vaga para o app: cidade/UF e o país quando a vaga não é no Brasil; remota sem endereço, o país.</summary>
    public static string? JobPlace(JobResponse job)
    {
        var place = Location(job.City, job.State);
        if (place is not null)
            return IsAbroad(job.Country) ? $"{place}, {CountryName(job.Country)}" : place;
        return job.IsRemote || job.Type == "Remote" || IsAbroad(job.Country) ? CountryName(job.Country) : null;
    }

    /// <summary>Tem algo de carreira internacional a mostrar (mesma regra da web, JobDisplay.HasInternationalInfo).</summary>
    public static bool HasInternationalInfo(JobResponse job) =>
        IsAbroad(job.Country) || job.RemoteScope is not null || job.OffersRelocation is not null || job.SponsorsVisa is not null;

    /// <summary>De onde trabalhar no remoto; sem informação diz isso, e remoto nunca vira "de qualquer país".</summary>
    public static string? RemoteScope(JobResponse job)
    {
        if (job.Type is not ("Remote" or "Hybrid"))
            return null;

        var countries = string.Join(", ", Monburan.Core.Services.InternationalJobRules.ParseCountries(job.EligibleCountries).Select(CountryName));
        return job.RemoteScope switch
        {
            "Global" => "De qualquer país",
            "Restricted" when countries.Length > 0 => $"Só de: {countries}",
            _ => "Não informado"
        };
    }

    /// <summary>Sim/não que pode não ter sido informado: nulo nunca vira "não".</summary>
    public static string YesNoUnknown(bool? value, string yes, string no) => value switch
    {
        true => yes,
        false => no,
        null => "Não informado"
    };

    /// <summary>Rótulos pt-BR do tipo de contrato (mesmos da web).</summary>
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

    /// <summary>Nível de formação do currículo; valor desconhecido aparece como veio.</summary>
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

    /// <summary>Situação do curso; valor desconhecido aparece como veio.</summary>
    public static string EducationStatus(string? status) => status switch
    {
        "Ongoing" => "Em andamento",
        "Completed" => "Concluído",
        "Dropped" => "Trancado",
        "Incomplete" => "Incompleto",
        _ => status ?? string.Empty
    };

    /// <summary>Nível do idioma; valor desconhecido aparece como veio.</summary>
    public static string LanguageLevel(string? level) => level switch
    {
        "Basic" => "Básico",
        "Intermediate" => "Intermediário",
        "Advanced" => "Avançado",
        "Fluent" => "Fluente",
        "Native" => "Nativo",
        _ => level ?? string.Empty
    };

    /// <summary>Nome do plano: o básico se chama "Free" no cadastro, mas a interface é em português.</summary>
    public static string PlanName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Equals("Free", StringComparison.OrdinalIgnoreCase)
            ? "Gratuito"
            : name.Trim();

    /// <summary>Valor em reais no formato pt-BR, sem casas decimais quando inteiro (ex.: "R$ 3.500").</summary>
    public static string Money(decimal value) =>
        "R$ " + value.ToString(value == decimal.Truncate(value) ? "N0" : "N2", PtBr);

    /// <summary>Período de um item do currículo ("03/2020 – atual", "2018 – 2021"...); <c>null</c> sem datas.</summary>
    public static string? Period(DateTime? start, DateTime? end, bool isCurrent = false)
    {
        var from = start?.ToString("MM/yyyy", PtBr);
        var to = isCurrent ? "atual" : end?.ToString("MM/yyyy", PtBr);
        return (from, to) switch
        {
            (not null, not null) => $"{from} – {to}",
            (not null, null) => $"desde {from}",
            (null, not null) => $"até {to}",
            _ => null
        };
    }

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
