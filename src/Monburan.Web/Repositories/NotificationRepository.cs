using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class NotificationRepository : IBaseRepository<Notification>
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NotificationRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Notification>> CreateAsync(Notification entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Notifications.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Notification>.Success(entity);
    }

    public async Task<RequestResult<Notification>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var notification = await context.Notifications.FindAsync(id);
        if (notification == null)
            return RequestResult<Notification>.EntityNotFound("Notification", id.Value, "Notificação não encontrada.");

        notification.IsDeleted = true;
        notification.DeletedAt = DateTime.UtcNow;
        
        context.Notifications.Update(notification);
        await context.SaveChangesAsync();
        return RequestResult<Notification>.Success(notification);
    }

    public async Task<RequestResult<IEnumerable<Notification>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var notifications = await context.Notifications
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Notification>>.Success(notifications);
    }

    public async Task<RequestResult<Notification>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (notification == null)
            return RequestResult<Notification>.EntityNotFound("Notification", id.Value, "Notificação não encontrada.");

        return RequestResult<Notification>.Success(notification);
    }

    public async Task<RequestResult<Notification>> UpdateAsync(Notification entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Notifications.Update(entity);
        await context.SaveChangesAsync();
        return RequestResult<Notification>.Success(entity);
    }
}
