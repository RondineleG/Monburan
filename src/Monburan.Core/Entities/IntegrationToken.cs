using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>
/// Token opaco de integração emitido pelo titular candidato. O valor real nunca é persistido:
/// apenas o hash SHA-256 permite validar o bearer nas rotas consentidas.
/// </summary>
public class IntegrationToken : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid CandidateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Escopos separados por espaço (ex.: <c>jobs:read readiness:read</c>).</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
