using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Notifications;

public class ListNotificationsByUserUseCase : IUseCase<string, List<NotificationResponse>>
{
    // Telas mostram as mais recentes; o badge usa COUNT no banco, sem esse teto
    public const int MaxNotifications = 200;

    private readonly INotificationRepository _notificationRepository;

    public ListNotificationsByUserUseCase(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<RequestResult<List<NotificationResponse>>> ExecuteAsync(string recipientId)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            return RequestResult<List<NotificationResponse>>.WithValidationError("ID do destinatário é obrigatório.", "RecipientId");

        var result = await _notificationRepository.ListByRecipientAsync(recipientId, MaxNotifications);
        if (result.Status != EResultStatus.Success)
            return RequestResult<List<NotificationResponse>>.FromResult(result);

        var response = result.Data!.Select(MapToResponse).ToList();

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
