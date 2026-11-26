using System.Text.Json.Serialization;

namespace Monburan.Core.DTOs.Request;

public class CreateApplicationRequest
{
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public string? Notes { get; set; }
    public Guid? ResumeId { get; set; }

    /// <summary>Decisão calculada no servidor; nunca vem do cliente.</summary>
    [JsonIgnore]
    public bool CaptureResumeSnapshot { get; set; }
}
