using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Notifications;

public class ListNotificationsByUserUseCase : IUseCase<string, List<NotificationResponse>>
{
    private readonly IBaseRepository<Entities.Notification> _notificationRepository;

    public ListNotificationsByUserUseCase(IBaseRepository<Entities.Notification> notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<RequestResult<List<NotificationResponse>>> ExecuteAsync(string recipientId)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            return RequestResult<List<NotificationResponse>>.WithValidationError("ID do destinatário é obrigatório.", "RecipientId");

        var result = await _notificationRepository.GetAllAsync(1, 1000);
        if (result.Status != EResultStatus.Success)
            return RequestResult<List<NotificationResponse>>.FromResult(result);

        var userNotifications = result.Data!
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        var response = userNotifications.Select(MapToResponse).ToList();

        return RequestResult<List<NotificationResponse>>.Success(response);
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
