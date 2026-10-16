using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Notifications;

public class MarkNotificationAsReadUseCase : IUseCase<Guid, NotificationResponse>
{
    private readonly IBaseRepository<Entities.Notification> _notificationRepository;

    public MarkNotificationAsReadUseCase(IBaseRepository<Entities.Notification> notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<RequestResult<NotificationResponse>> ExecuteAsync(Guid notificationId)
    {
        if (notificationId == Guid.Empty)
            return RequestResult<NotificationResponse>.WithValidationError("ID da notificação é inválido.", "NotificationId");

        var getResult = await _notificationRepository.GetByIdAsync(Id.Create(notificationId));
        if (getResult.Status != EResultStatus.Success || getResult.Data == null)
            return RequestResult<NotificationResponse>.EntityNotFound("Notification", notificationId, "Notificação não encontrada.");

        var notification = getResult.Data;
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        var result = await _notificationRepository.UpdateAsync(notification);
        if (result.Status != EResultStatus.Success)
            return RequestResult<NotificationResponse>.FromResult(result);

        return RequestResult<NotificationResponse>.Success(MapToResponse(result.Data!));
    }

    private static NotificationResponse MapToResponse(Entities.Notification notification) => new()
    {
        Id = notification.Id.Value,
        Title = notification.Title ?? string.Empty,
        Message = notification.Message ?? string.Empty,
        RecipientId = notification.RecipientId,
        Type = notification.Type,
        IsRead = notification.IsRead,
        ReadAt = notification.ReadAt,
        CreatedAt = notification.CreatedAt
    };
}
