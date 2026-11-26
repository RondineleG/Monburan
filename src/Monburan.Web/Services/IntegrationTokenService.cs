using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

/// <summary>
/// Emissão, listagem, revogação e validação de tokens opacos de integração. O segredo só existe
/// na resposta da criação/em memória; no banco fica exclusivamente o hash SHA-256.
/// </summary>
public sealed class IntegrationTokenService(ApplicationDbContext context, TimeProvider timeProvider)
{
    public const string JobsScope = "jobs:read";
    public const string ReadinessScope = "readiness:read";
    private static readonly string[] AllowedScopes = [JobsScope, ReadinessScope];

    public async Task<IReadOnlyList<IntegrationToken>> ListActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.IntegrationTokens.AsNoTracking()
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .OrderByDescending(token => token.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IntegrationToken? Token, string? Secret, string? Error)> CreateAsync(
        Guid userId,
        Guid candidateId,
        string name,
        string[] requestedScopes,
        int expiresInDays,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length is 0 or > 80)
            return (null, null, "Informe um nome de 1 a 80 caracteres.");
        if (expiresInDays is < 1 or > 90)
            return (null, null, "A expiração deve ficar entre 1 e 90 dias.");

        var scopes = NormalizeScopes(requestedScopes);
        if (scopes is null)
            return (null, null, "Escopo de integração inválido.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var secret = "mbi_" + Base64Url(RandomNumberGenerator.GetBytes(32));
        var token = new IntegrationToken
        {
            UserId = userId,
            CandidateId = candidateId,
            Name = trimmedName,
            TokenHash = Hash(secret),
            Scopes = scopes,
            ExpiresAt = now.AddDays(expiresInDays),
            TenantId = "default",
            CreatedBy = createdBy
        };
        context.IntegrationTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
        return (token, secret, null);
    }

    public async Task<bool> RevokeAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return false;

        var token = await context.IntegrationTokens
            .SingleOrDefaultAsync(row => row.Id == Id.Create(id) && row.UserId == userId && row.RevokedAt == null, cancellationToken);
        if (token is null)
            return false;

        token.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IntegrationToken?> ValidateAsync(string? secret, string requiredScope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith("mbi_", StringComparison.Ordinal))
            return null;

        var token = await context.IntegrationTokens
            .SingleOrDefaultAsync(row => row.TokenHash == Hash(secret), cancellationToken);
        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
            return null;
        return token.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(requiredScope) ? token : null;
    }

    public async Task MarkUsedAsync(IntegrationToken token, CancellationToken cancellationToken)
    {
        token.LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);
    }

    public static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static string? NormalizeScopes(string[] requested)
    {
        var scopes = requested.Select(scope => scope.Trim().ToLowerInvariant())
            .Where(scope => scope.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToArray();
        return scopes.Length > 0 && scopes.All(AllowedScopes.Contains) ? string.Join(' ', scopes) : null;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
