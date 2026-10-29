using System.Security.Claims;

namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Consulta de feature flags para a conta atual. Flag inexistente conta como desligada. A tela sempre confere também no
/// servidor: esconder um botão não protege o recurso.
/// </summary>
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string feature, ClaimsPrincipal? user, CancellationToken cancellationToken = default);

    /// <summary>Nomes das flags ligadas para a conta (o app recebe esta lista).</summary>
    Task<IReadOnlyList<string>> EnabledFeaturesAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default);

    /// <summary>Descarta o cache: chamado quando uma flag, um grupo ou a participação de alguém muda.</summary>
    void Invalidate(Guid? userId = null);
}
