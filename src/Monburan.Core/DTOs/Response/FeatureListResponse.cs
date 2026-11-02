namespace Monburan.Core.DTOs.Response;

/// <summary>Flags ligadas para a conta que fez a chamada (ou só as liberadas para todos, se anônima).</summary>
public class FeatureListResponse
{
    public List<string> Features { get; set; } = [];
}
