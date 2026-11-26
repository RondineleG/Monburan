using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Perfil de carreira do candidato: um por candidato, base das recomendações de vagas.</summary>
public class CareerProfile : BaseEntity
{
    public Id CandidateId { get; set; } = Id.Empty;

    public string? Category { get; set; }

    public string? City { get; set; }

    public EContractType? ContractType { get; set; }

    public string? DesiredRole { get; set; }

    public decimal? MinimumSalary { get; set; }

    public string? Mode { get; set; }

    public string? State { get; set; }

    /// <summary>
    /// Países (ISO 3166-1 alpha-2, CSV) que o candidato quer ver nas vagas internacionais.
    /// Vazio significa "todos os países fora do Brasil".
    /// </summary>
    public string? PreferredCountries { get; set; }

    /// <summary>Países de interesse para trabalho internacional (códigos ISO 3166-1 alfa-2, ex.: "ES", "PT").</summary>
    public List<string> InterestedCountries { get; set; } = [];

    /// <summary>Disponibilidade autodeclarada para remoto internacional ou realocação.</summary>
    public EInternationalMobility Mobility { get; set; } = EInternationalMobility.NotInformed;

    /// <summary>Autorização de trabalho autodeclarada — opcional e sem veredito jurídico.</summary>
    public EWorkAuthorizationStatus WorkAuthorizationStatus { get; set; } = EWorkAuthorizationStatus.NotInformed;
}
