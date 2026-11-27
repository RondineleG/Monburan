using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Job : BaseEntity
{
    /// <summary>Anunciante que publicou a vaga. Obrigatório nas vagas diretas; nulo nas externas, que não têm anunciante.</summary>
    public virtual Advertiser? Advertiser { get; set; }

    public Id? AdvertiserId { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = [];

    /// <summary>Canal de candidatura. Vagas diretas usam InternalPlatform; vagas externas usam um dos canais externos.</summary>
    public EApplicationChannel ApplicationChannel { get; set; } = EApplicationChannel.InternalPlatform;

    /// <summary>Contato do canal (número do WhatsApp ou e-mail), quando o canal exige.</summary>
    public string? ApplicationContact { get; set; }

    public string? ApplicationInstructions { get; set; }

    /// <summary>Link do site onde a candidatura é feita, quando o canal é ExternalUrl.</summary>
    public string? ApplicationUrl { get; set; }

    public string? Benefits { get; set; }

    public JobBoost? Boost { get; set; }

    public string? Category { get; set; }

    /// <summary>Código CBO da ocupação, quando informado pela fonte.</summary>
    public string? CboCode { get; set; }

    /// <summary>Tipo de contratação; nulo nas vagas criadas antes deste campo existir.</summary>
    public EContractType? ContractType { get; set; }

    /// <summary>Empresa contratante. Em vagas diretas é o próprio anunciante; em vagas externas é informada pela fonte.</summary>
    public string? CompanyName { get; set; }

    public JobCounters Counters { get; set; } = new();

    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>Prazo de candidatura (validade da vaga).</summary>
    public DateTime ExpirationDate { get; set; }

    public JobHighlight? Highlight { get; set; }

    /// <summary>Data da última conferência da vaga externa com a fonte original.</summary>
    public DateTime? LastVerifiedAt { get; set; }

    public JobLocation? Location { get; set; }

    public ModerationInfo? Moderation { get; set; }

    public int NumberOfOpenings { get; set; } = 1;

    public string? Observations { get; set; }

    public EPublicationType PublicationType { get; set; } = EPublicationType.Direct;

    public string? Requirements { get; set; }

    public string? Responsibilities { get; set; }

    public SalaryRange? Salary { get; set; }

    /// <summary>Abrangência do trabalho remoto; nulo = não informado. Só vale para vaga remota ou híbrida.</summary>
    public ERemoteScope? RemoteScope { get; set; }

    /// <summary>Países de onde se pode trabalhar (códigos ISO separados por vírgula), quando o remoto é restrito.</summary>
    public string? EligibleCountries { get; set; }

    /// <summary>A empresa oferece ajuda de relocação; nulo = não informado (diferente de "não").</summary>
    public bool? OffersRelocation { get; set; }

    /// <summary>A empresa patrocina visto de trabalho; nulo = não informado (diferente de "não").</summary>
    public bool? SponsorsVisa { get; set; }

    /// <summary>Nome da fonte da vaga externa (ex.: nome do site ou da agência).</summary>
    public string? SourceName { get; set; }

    /// <summary>Endereço da publicação original da vaga externa.</summary>
    public string? SourceUrl { get; set; }

    public EJobStatus Status { get; set; } = EJobStatus.Active;

    public string? Tags { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;
}
