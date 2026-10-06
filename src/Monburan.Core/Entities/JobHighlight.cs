namespace Monburan.Core.Entities;

public class JobHighlight
{
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = false;

    public DateTime? StartDate { get; set; }
}