using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class CreateNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public ENotificationType Type { get; set; } = ENotificationType.System;

    public Guid? JobAlertId { get; set; }

    public Guid? JobId { get; set; }
}
