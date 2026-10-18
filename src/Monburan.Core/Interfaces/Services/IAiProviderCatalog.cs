namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Foto, em memória, dos provedores de IA cadastrados. O cliente de IA e a tela dos recursos consultam isto a cada
/// chamada sem ir ao banco; a foto é refeita depois de cada alteração do administrador e de tempos em tempos.
/// </summary>
public interface IAiProviderCatalog
{
    /// <summary>Provedor padrão ativo, ou <c>null</c> quando não há (a IA fica desligada).</summary>
    AiProviderSettings? Default { get; }

    /// <summary>Todos os provedores cadastrados, ativos ou não.</summary>
    IReadOnlyList<AiProviderSettings> All { get; }

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
