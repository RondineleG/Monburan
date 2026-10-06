using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

public class ApplicationStatusHistory : BaseEntity
{
    public Application? Application { get; set; } = default!;

    public Id ApplicationId { get; set; } = Id.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public string? Responsible { get; set; }

    public EApplicationStatus Status { get; set; }
}