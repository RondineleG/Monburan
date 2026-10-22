using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NotificationRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Notification>> CreateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Notifications.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Notification>.Success(entity);
    }

    public async Task<RequestResult<Notification>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await context.Notifications.FindAsync([id], cancellationToken);
        if (notification == null)
            return RequestResult<Notification>.EntityNotFound("Notification", id.Value, "Notificação não encontrada.");

        notification.IsDeleted = true;
        notification.DeletedAt = DateTime.UtcNow;
        
        context.Notifications.Update(notification);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Notification>.Success(notification);
    }

    public async Task<RequestResult<IEnumerable<Notification>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var notifications = await context.Notifications
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Notification>>.Success(notifications);
    }

    public async Task<RequestResult<Notification>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

        if (notification == null)
            return RequestResult<Notification>.EntityNotFound("Notification", id.Value, "Notificação não encontrada.");

        return RequestResult<Notification>.Success(notification);
    }

    public async Task<RequestResult<Notification>> UpdateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Notifications.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Notification>.Success(entity);
    }

    public async Task<RequestResult<IEnumerable<Notification>>> ListByRecipientAsync(string recipientId, int take, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var notifications = await context.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDeleted && n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Notification>>.Success(notifications);
    }

    public async Task<RequestResult<int>> MarkAllAsReadAsync(string recipientId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var unread = await context.Notifications
            .Where(n => !n.IsDeleted && !n.IsRead && n.RecipientId == recipientId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<int>.Success(unread.Count);
    }
}
