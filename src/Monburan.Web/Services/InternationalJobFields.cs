using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;

namespace Monburan.Web.Services;

/// <summary>
/// Aplica no servidor a flag <c>carreira-internacional</c> sobre os campos novos da vaga (abrangência do remoto, países
/// elegíveis, relocação e visto). Desligada, a criação descarta os campos e a edição mantém o que já estava gravado:
/// esconder o campo na tela não basta, e a flag desligada não pode apagar dados.
/// </summary>
public static class InternationalJobFields
{
    public static void DropUnlessEnabled(CreateJobRequest request, bool enabled)
    {
        if (enabled)
            return;

        request.RemoteScope = null;
        request.EligibleCountries = null;
        request.OffersRelocation = null;
        request.SponsorsVisa = null;
    }

    public static void KeepExistingUnlessEnabled(UpdateJobRequest request, JobResponse existing, bool enabled)
    {
        if (enabled)
            return;

        request.RemoteScope = ParseScope(existing.RemoteScope);
        request.EligibleCountries = existing.EligibleCountries;
        request.OffersRelocation = existing.OffersRelocation;
        request.SponsorsVisa = existing.SponsorsVisa;
    }

    public static ERemoteScope? ParseScope(string? value) =>
        Enum.TryParse<ERemoteScope>(value, out var scope) ? scope : null;
}
