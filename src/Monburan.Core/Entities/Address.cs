using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

public class Address : BaseEntity
{
    public string? City { get; set; }

    public string? Complement { get; set; }

    public string? Country { get; set; } = "BR";

    public string? District { get; set; }

    public string? Number { get; set; }

    public string? State { get; set; }

    public string? Street { get; set; }

    public string? ZipCode { get; set; }
}