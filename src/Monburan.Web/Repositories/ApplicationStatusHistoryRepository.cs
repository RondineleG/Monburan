using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class ApplicationStatusHistoryRepository : IApplicationStatusHistoryRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ApplicationStatusHistoryRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<ApplicationStatusHistory>> CreateAsync(ApplicationStatusHistory entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.ApplicationStatusHistory.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<ApplicationStatusHistory>.Success(entity);
    }

    public async Task<RequestResult<IEnumerable<ApplicationStatusHistory>>> ListByApplicationAsync(Guid applicationId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var applicationKey = Id.Create(applicationId);
        var history = await context.ApplicationStatusHistory
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.ApplicationId == applicationKey)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<ApplicationStatusHistory>>.Success(history);
    }
}
