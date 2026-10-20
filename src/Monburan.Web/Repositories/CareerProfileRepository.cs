using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class CareerProfileRepository : ICareerProfileRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CareerProfileRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<CareerProfile?>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidateKey = Id.Create(candidateId);
        var profile = await context.CareerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.CandidateId == candidateKey, cancellationToken);

        return RequestResult<CareerProfile?>.Success(profile);
    }

    public async Task<RequestResult<CareerProfile>> CreateAsync(CareerProfile entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.CareerProfiles.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<CareerProfile>.Success(entity);
    }

    public async Task<RequestResult<CareerProfile>> UpdateAsync(CareerProfile entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.CareerProfiles.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<CareerProfile>.Success(entity);
    }
}
