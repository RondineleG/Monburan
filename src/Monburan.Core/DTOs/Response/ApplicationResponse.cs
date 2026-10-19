namespace Monburan.Core.DTOs.Response;

public class ApplicationResponse
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public string? CandidateEmail { get; set; }
    public Guid JobId { get; set; }
    public string? JobTitle { get; set; }
    public string? AdvertiserName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? SelectedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? Notes { get; set; }
    public Guid? ResumeId { get; set; }
    public DateTime CreatedAt { get; set; }
}
