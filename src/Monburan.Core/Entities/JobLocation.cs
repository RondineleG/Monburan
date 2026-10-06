namespace Monburan.Core.Entities;

public class JobLocation
{
    public string? City { get; set; }

    public string Country { get; set; } = "BR";

    public bool IsRemote { get; set; }

    public bool ShowAddress { get; set; } = true;

    public string? State { get; set; }

    public string Type { get; set; } = "OnSite";
}