using System.Security.Cryptography;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.ValueObjects;
using Monburan.Core.Enums;

namespace Monburan.Web.Services;

/// <summary>Emissão, validação e revogação de tokens de integração por escopo.</summary>
public interface IIntegrationTokenService
{
    /// <summary>Escopos suportados hoje pela integração externa (HiMarshall).</summary>
    static readonly string[] SupportedScopes = ["readiness:read", "jobs:read"];

    /// <summary>Cria um token novo; devolve a entidade e o valor em claro (mostrado uma vez).</summary>
    Task<(IntegrationToken Token, string RawValue)> CreateAsync(
        Guid userId, string name, IReadOnlyCollection<string> scopes, int expiresInDays,
        CancellationToken cancellationToken = default);

    /// <summary>Valida o valor em claro contra um escopo exigido; devolve o token ativo ou null.</summary>
    Task<IntegrationToken?> ValidateAsync(string rawToken, string requiredScope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationToken>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
}

public sealed class IntegrationTokenService(IIntegrationTokenRepository repository) : IIntegrationTokenService
{
    private const string TokenPrefix = "mbi_";

    public async Task<(IntegrationToken Token, string RawValue)> CreateAsync(
        Guid userId, string name, IReadOnlyCollection<string> scopes, int expiresInDays,
        CancellationToken cancellationToken = default)
    {
        var rawValue = TokenPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(30))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var token = new IntegrationToken
        {
            UserId = userId,
            Name = name.Trim(),
            Prefix = rawValue[..Math.Min(12, rawValue.Length)],
            TokenHash = Hash(rawValue),
            Scopes = string.Join(',', scopes),
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Clamp(expiresInDays, 1, 365))
        };

        await repository.CreateAsync(token, cancellationToken);
        return (token, rawValue);
    }

    public async Task<IntegrationToken?> ValidateAsync(string rawToken, string requiredScope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || !rawToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return null;

        var token = await repository.FindByTokenHashAsync(Hash(rawToken), cancellationToken);
        if (token is null || !token.HasScope(requiredScope))
            return null;

        try { await repository.TouchLastUsedAsync(token.Id.Value, cancellationToken); }
        catch { /* última utilização é informativa; não derruba a chamada */ }

        return token;
    }

    public Task<IReadOnlyList<IntegrationToken>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
        => repository.ListByUserIdAsync(userId, cancellationToken);

    public async Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var result = await repository.RevokeAsync(Id.Create(tokenId), userId, cancellationToken);
        return result.Status == EResultStatus.Success;
    }

    internal static string Hash(string rawValue)
        => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawValue)));
}
