using Monburan.Core.Enums;

namespace Monburan.Core.Entities;

public class SalaryRange
{
    public string Currency { get; set; } = "BRL";

    public bool Display { get; set; } = true;

    public decimal? Max { get; set; }

    public decimal? Min { get; set; }

    /// <summary>Período do valor (por hora, por mês...); nulo quando a vaga não informa.</summary>
    public ESalaryPeriod? Period { get; set; }
}