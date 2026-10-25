using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>
/// Canal de candidatura da vaga: rótulo da ação, aviso e destino externo. A decisão de canal vem do domínio
/// (<c>PublicationType</c> e <c>ApplicationChannel</c>); aqui só se traduz para o texto e o link da tela.
/// </summary>
public static class JobChannel
{
    public const string Direct = "Direct";
    public const string External = "External";

    public const string InternalPlatform = "InternalPlatform";
    public const string ExternalUrl = "ExternalUrl";
    public const string WhatsApp = "WhatsApp";
    public const string Email = "Email";
    public const string OnSite = "OnSite";

    public static bool IsExternal(JobResponse job) => job.PublicationType == External;

    /// <summary>Identificação visível do tipo de vaga.</summary>
    public static string PublicationLabel(JobResponse job) => IsExternal(job) ? "Vaga externa" : "Vaga direta";

    /// <summary>Empresa contratante: a informada pela fonte (externa) ou o próprio anunciante (direta).</summary>
    public static string? CompanyName(JobResponse job) => string.IsNullOrWhiteSpace(job.CompanyName) ? job.AdvertiserName : job.CompanyName;

    /// <summary>Texto do botão principal de candidatura, conforme o canal.</summary>
    public static string CtaLabel(JobResponse job) => job.ApplicationChannel switch
    {
        ExternalUrl => "Candidatar-se no site da empresa",
        WhatsApp => "Enviar mensagem pelo WhatsApp",
        Email => "Enviar currículo por e-mail",
        OnSite => "Ver instruções presenciais",
        _ => "Candidatar-se na Monburan"
    };

    /// <summary>Aviso exibido antes da ação quando a candidatura sai da Monburan; nulo para vagas diretas.</summary>
    public static string? Notice(JobResponse job) => IsExternal(job)
        ? "Você será direcionado para outro canal. A candidatura não é acompanhada dentro da Monburan."
        : null;

    /// <summary>
    /// Destino do botão externo. Nulo quando o canal não tem link (candidatura interna ou presencial, que mostra as instruções).
    /// </summary>
    public static string? ExternalHref(JobResponse job) => job.ApplicationChannel switch
    {
        ExternalUrl when !string.IsNullOrWhiteSpace(job.ApplicationUrl) => job.ApplicationUrl,
        WhatsApp when !string.IsNullOrWhiteSpace(job.ApplicationContact) => WhatsAppLink(job.ApplicationContact),
        Email when !string.IsNullOrWhiteSpace(job.ApplicationContact) => $"mailto:{job.ApplicationContact}",
        _ => null
    };

    /// <summary>Converte o número de WhatsApp informado (com DDD) no link wa.me, acrescentando o código do Brasil.</summary>
    public static string WhatsAppLink(string contact)
    {
        var digits = new string(contact.Where(char.IsDigit).ToArray());
        if (digits.Length == 11)
            digits = "55" + digits;
        return $"https://wa.me/{digits}";
    }
}
