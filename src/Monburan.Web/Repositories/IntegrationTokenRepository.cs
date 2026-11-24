using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Repositories;

public class IntegrationTokenRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : IIntegrationTokenRepository
{
    public async Task<RequestResult<IntegrationToken>> CreateAsync(IntegrationToken token, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.IntegrationTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<IntegrationToken>.Success(token);
    }

    public async Task<IntegrationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.IntegrationTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsDeleted, cancellationToken);
    }

    public async Task<IntegrationToken?> FindByRequestIdAsync(Guid requestId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.IntegrationTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.RequestId == requestId && t.UserId == userId && !t.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationToken>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.IntegrationTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestResult<IntegrationToken>> RevokeAsync(Id id, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var token = await context.IntegrationTokens
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted, cancellationToken);
        if (token is null)
            return RequestResult<IntegrationToken>.EntityNotFound("IntegrationToken", id.Value, "Token não encontrado.");

        token.RevokedAt = DateTime.UtcNow;
        context.IntegrationTokens.Update(token);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<IntegrationToken>.Success(token);
    }

    public async Task TouchLastUsedAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.IntegrationTokens
            .Where(t => t.Id == Id.Create(tokenId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.LastUsedAt, DateTime.UtcNow), cancellationToken);
    }
}
