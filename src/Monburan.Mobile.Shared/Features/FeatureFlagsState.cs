using Monburan.Mobile.Shared.Api;
using Monburan.Mobile.Shared.Auth;

namespace Monburan.Mobile.Shared.Features;

/// <summary>
/// Flags ligadas para a conta do app, lidas de <c>GET /api/v1/features</c>. Recarrega ao entrar e ao sair da conta. Sem
/// resposta do servidor (offline), mantém a última lista; sem nenhuma lista, tudo atrás de flag fica desligado. O servidor
/// confere de novo em cada endpoint: esta lista só decide o que aparece na tela.
/// </summary>
public sealed class FeatureFlagsState
{
    private readonly MonburanApiClient _api;
    private HashSet<string> _enabled = new(StringComparer.Ordinal);

    public FeatureFlagsState(MonburanApiClient api, AuthStateService auth)
    {
        _api = api;
        auth.Changed += () => _ = RefreshAsync();
    }

    public event Action? Changed;

    public bool IsEnabled(string feature) => _enabled.Contains(feature);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _api.GetFeaturesAsync(cancellationToken);
            _enabled = new HashSet<string>(response.Features, StringComparer.Ordinal);
            Changed?.Invoke();
        }
        catch (ApiException)
        {
            // Offline ou servidor indisponível: fica a última lista conhecida
        }
    }
}
