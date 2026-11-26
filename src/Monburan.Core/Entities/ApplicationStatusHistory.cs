using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

public class ApplicationStatusHistory : BaseEntity
{
    public const int MaxNoteLength = 1000;
    public const int MaxResponsibleLength = 100;
    public const int MaxActorUserIdLength = 100;

    public Application? Application { get; set; } = default!;

    public Id ApplicationId { get; set; } = Id.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public string? Responsible { get; set; }

    /// <summary>Identidade autenticada que executou a transição.</summary>
    public string? ActorUserId { get; set; }

    /// <summary>Versão sequencial do estado da candidatura; a candidatura inicial é a versão 1.</summary>
    public int Version { get; set; } = 1;

    public EApplicationStatus Status { get; set; }
}