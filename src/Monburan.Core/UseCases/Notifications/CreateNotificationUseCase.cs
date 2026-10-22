using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Notifications;

public class CreateNotificationUseCase : IUseCase<CreateNotificationRequest, NotificationResponse>
{
    private readonly IBaseRepository<Notification> _notificationRepository;

    public CreateNotificationUseCase(IBaseRepository<Notification> notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<RequestResult<NotificationResponse>> ExecuteAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return RequestResult<NotificationResponse>.WithValidationError("Título é obrigatório.", "Title");

        if (string.IsNullOrWhiteSpace(request.Message))
            return RequestResult<NotificationResponse>.WithValidationError("Mensagem é obrigatória.", "Message");

        if (string.IsNullOrWhiteSpace(request.RecipientId))
            return RequestResult<NotificationResponse>.WithValidationError("Destinatário é obrigatório.", "RecipientId");

        var notification = new Notification
        {
            Id = Id.Create(Guid.NewGuid()),
            Title = request.Title,
            Message = request.Message,
            RecipientId = request.RecipientId,
            Type = request.Type,
            IsRead = false,
            TenantId = "default"
        };

        var result = await _notificationRepository.CreateAsync(notification, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<NotificationResponse>.FromResult(result);

        return RequestResult<NotificationResponse>.Success(MapToResponse(result.Data!));
    }

    private static NotificationResponse MapToResponse(Notification notification) => new()
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
