using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class Notification : BaseEntity
{
    public bool IsRead { get; set; } = false;

    public string? Message { get; set; }

    public DateTime? ReadAt { get; set; }

    [Required]
    public string RecipientId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public ENotificationType Type { get; set; } = ENotificationType.System;
}