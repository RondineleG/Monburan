using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public sealed class CandidateLanguageRepository : ICandidateLanguageRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CandidateLanguageRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<IEnumerable<CandidateLanguage>>> GetByCandidateIdAsync(Id candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var languages = await context.CandidateLanguages
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.CandidateId == candidateId)
            .OrderBy(item => item.LanguageTag)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<CandidateLanguage>>.Success(languages);
    }

    public async Task<RequestResult<IEnumerable<CandidateLanguage>>> ReplaceForCandidateAsync(
        Id candidateId, IReadOnlyList<CandidateLanguage> languages, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existing = await context.CandidateLanguages
            .Where(item => !item.IsDeleted && item.CandidateId == candidateId)
            .ToListAsync(cancellationToken);

        // Soft-delete para manter histórico/auditoria igual ao resto do sistema.
        foreach (var item in existing)
            item.IsDeleted = true;
        context.CandidateLanguages.AddRange(languages);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RequestResult<IEnumerable<CandidateLanguage>>.Success(languages);
    }
}
