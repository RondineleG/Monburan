using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Response;

public class InternationalPreferencesResponse
{
    public List<string> InterestedCountries { get; set; } = [];

    public EInternationalMobility Mobility { get; set; } = EInternationalMobility.NotInformed;

    public EWorkAuthorizationStatus WorkAuthorizationStatus { get; set; } = EWorkAuthorizationStatus.NotInformed;
}
