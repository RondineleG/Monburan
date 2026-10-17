using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Notifications;

/// <summary>
/// Marca como lidas, em uma única operação, todas as notificações não lidas do destinatário.
/// </summary>
public class MarkAllNotificationsAsReadUseCase : IUseCase<string, int>
{
    private readonly INotificationRepository _notificationRepository;

    public MarkAllNotificationsAsReadUseCase(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public Task<RequestResult<int>> ExecuteAsync(string recipientId)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            return Task.FromResult(RequestResult<int>.WithValidationError("ID do destinatário é obrigatório.", "RecipientId"));

        return _notificationRepository.MarkAllAsReadAsync(recipientId);
    }
}
