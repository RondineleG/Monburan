using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public sealed class ResumeVariantRepository : IResumeVariantRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ResumeVariantRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<IEnumerable<ResumeVariant>>> ListByResumeAsync(Id resumeId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var variants = await context.ResumeVariants
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive && item.ResumeId == resumeId)
            .OrderBy(item => item.Locale)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<ResumeVariant>>.Success(variants);
    }

    public async Task<RequestResult<ResumeVariant?>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var variant = await context.ResumeVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);

        return RequestResult<ResumeVariant?>.Success(variant);
    }

    public async Task<RequestResult<ResumeVariant>> CreateAsync(ResumeVariant entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.ResumeVariants.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<ResumeVariant>.Success(entity);
    }

    public async Task<RequestResult<ResumeVariant>> UpdateAsync(ResumeVariant entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tracked = await context.ResumeVariants
            .FirstOrDefaultAsync(item => item.Id == entity.Id, cancellationToken);
        if (tracked is null)
            return RequestResult<ResumeVariant>.EntityNotFound("ResumeVariant", entity.Id.Value, "Variante de currículo não encontrada.");

        tracked.Locale = entity.Locale;
        tracked.OverridesJson = entity.OverridesJson;
        tracked.PendingSuggestionsJson = entity.PendingSuggestionsJson;
        tracked.IsActive = entity.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<ResumeVariant>.Success(tracked);
    }

    public async Task<RequestResult<ResumeVariant?>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var tracked = await context.ResumeVariants
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (tracked is null)
            return RequestResult<ResumeVariant?>.Success(null);

        tracked.IsDeleted = true;
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<ResumeVariant?>.Success(tracked);
    }
}
