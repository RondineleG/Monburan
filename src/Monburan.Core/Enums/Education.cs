namespace Monburan.Core.Enums;

public class Education
{
    public string Course { get; set; } = string.Empty;

    public DateTime? EndDate { get; set; }

    public string Institution { get; set; } = string.Empty;

    public EEducationLevel Level { get; set; }

    public DateTime? StartDate { get; set; }

    public EEducationStatus Status { get; set; }
}