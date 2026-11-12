using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

/// <summary>Preferências internacionais autodeclaradas do candidato (MI-01).</summary>
public class InternationalPreferencesRequest
{
    /// <summary>Países de interesse em ISO 3166-1 alfa-2 (ex.: "ES", "PT"). Máx. 20.</summary>
    public List<string> InterestedCountries { get; set; } = [];

    public EInternationalMobility Mobility { get; set; } = EInternationalMobility.NotInformed;

    /// <summary>Autorização de trabalho autodeclarada — opcional, sem veredito jurídico.</summary>
    public EWorkAuthorizationStatus WorkAuthorizationStatus { get; set; } = EWorkAuthorizationStatus.NotInformed;
}
