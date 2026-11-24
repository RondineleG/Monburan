using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>
/// Token de integração emitido pelo usuário para consumidores externos (ex.: HiMarshall).
/// Guarda só o hash SHA-256 do valor: o token em claro aparece uma única vez na criação.
/// Escopos em CSV (ex.: <c>readiness:read,jobs:read</c>) — o formato definido pelo
/// consumidor, sem enum fechado, para não exigir deploy a cada escopo novo.
/// </summary>
public class IntegrationToken : BaseEntity
{
    /// <summary>Id do usuário dono do token (ApplicationUser).</summary>
    public Guid UserId { get; set; }

    /// <summary>Rótulo amigável escolhido pelo usuário (ex.: "HiMarshall").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Primeiros caracteres do token, para identificação na listagem.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) do token em claro. Nunca guarda o valor original.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Escopos concedidos, separados por vírgula (ex.: "readiness:read,jobs:read").</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>Id único do envio do formulário web: deduplica reemissão (F5/replay) mesmo após restart.</summary>
    public Guid? RequestId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    /// <summary>Token utilizável: não revogado e dentro da validade.</summary>
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow && !IsDeleted;

    /// <summary>Escopos como lista, tolerando espaços e duplicados.</summary>
    public IReadOnlyList<string> ScopeList => Scopes
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Verifica se o token (ativo) concede o escopo exigido.</summary>
    public bool HasScope(string scope)
        => IsActive && ScopeList.Contains(scope, StringComparer.OrdinalIgnoreCase);
}
