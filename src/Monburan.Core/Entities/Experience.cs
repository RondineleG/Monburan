namespace Monburan.Core.Entities;

public class Experience
{
    public string Company { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsCurrent { get; set; } = false;

    public string Position { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
}