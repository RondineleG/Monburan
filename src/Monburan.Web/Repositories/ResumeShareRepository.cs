using Microsoft.EntityFrameworkCore;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Repositories;

public class ResumeShareRepository : IResumeShareRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ResumeShareRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<ResumeShare>> CreateAsync(ResumeShare share, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.ResumeShares.Add(share);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<ResumeShare>.Success(share);
    }

    public async Task<RequestResult<ResumeShare>> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var share = await WithResume(context.ResumeShares.AsNoTracking())
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && !s.IsDeleted, cancellationToken);

        return share is null
            ? RequestResult<ResumeShare>.EntityNotFound("ResumeShare", "token", "Link não encontrado.")
            : RequestResult<ResumeShare>.Success(share);
    }

    public async Task<RequestResult<ResumeShare>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var share = await WithResume(context.ResumeShares)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);

        return share is null
            ? RequestResult<ResumeShare>.EntityNotFound("ResumeShare", id.Value, "Link não encontrado.")
            : RequestResult<ResumeShare>.Success(share);
    }

    public async Task<RequestResult<IReadOnlyList<ResumeShare>>> ListByResumeAsync(Id resumeId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var shares = await context.ResumeShares.AsNoTracking()
            .Where(s => s.ResumeId == resumeId && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        return RequestResult<IReadOnlyList<ResumeShare>>.Success(shares);
    }

    public async Task<RequestResult<ResumeShare>> UpdateAsync(ResumeShare share, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tracked = await context.ResumeShares.FirstOrDefaultAsync(s => s.Id == share.Id, cancellationToken);
        if (tracked is null)
            return RequestResult<ResumeShare>.EntityNotFound("ResumeShare", share.Id.Value, "Link não encontrado.");

        tracked.RevokedAt = share.RevokedAt;
        tracked.ExpiresAt = share.ExpiresAt;
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<ResumeShare>.Success(tracked);
    }

    /// <summary>Currículo com candidato e habilidades. Experiências, formação e idiomas são colunas próprias, já carregadas.</summary>
    private static IQueryable<ResumeShare> WithResume(IQueryable<ResumeShare> query) => query
        .Include(s => s.Resume!)
        .ThenInclude(r => r.Candidate)
        .Include(s => s.Resume!)
        .ThenInclude(r => r.Skills);
}
