namespace Monburan.Core.Entities;

public class JobBoost
{
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = false;

    public string? Level { get; set; }

    public DateTime StartDate { get; set; }
}