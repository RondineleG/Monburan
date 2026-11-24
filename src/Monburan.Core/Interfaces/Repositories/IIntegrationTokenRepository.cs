using Monburan.Core.Entities;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>Acesso aos tokens de integração. Consulta pelo hash, nunca pelo valor em claro.</summary>
public interface IIntegrationTokenRepository
{
    Task<RequestResult<IntegrationToken>> CreateAsync(IntegrationToken token, CancellationToken cancellationToken = default);

    Task<IntegrationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Busca o token já emitido por um envio de formulário (dedup de replay).</summary>
    Task<IntegrationToken?> FindByRequestIdAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationToken>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RequestResult<IntegrationToken>> RevokeAsync(Id id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marca o uso do token; falha não derruba a requisição validada.</summary>
    Task TouchLastUsedAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
