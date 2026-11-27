using System.Text.Json;
using System.Text.Json.Serialization;

using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Fotografia imutável de uma revisão da vaga, com autor interno e estado resultante.</summary>
public class JobRevision : BaseEntity
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Id JobId { get; set; } = null!;

    public virtual Job? Job { get; set; }

    public int Revision { get; set; }

    public EJobRevisionAction Action { get; set; }

    public EJobStatus Status { get; set; }

    /// <summary>Identificador interno do autor da operação; a API traduz o papel sem expor ids de terceiros.</summary>
    public string ActorId { get; set; } = string.Empty;

    public string SnapshotJson { get; set; } = string.Empty;

    public static JobRevision Capture(Job job, int revision, EJobRevisionAction action, string actorId)
    {
        return new JobRevision
        {
            JobId = job.Id,
            Revision = revision,
            Action = action,
            Status = job.Status,
            ActorId = actorId,
            TenantId = job.TenantId,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            SnapshotJson = Snapshot(job)
        };
    }

    private static string Snapshot(Job job)
    {
        return JsonSerializer.Serialize(new
        {
            job.Title,
            job.Description,
            job.Requirements,
            job.Responsibilities,
            job.Benefits,
            job.ApplicationInstructions,
            job.NumberOfOpenings,
            job.Observations,
            job.Category,
            job.Tags,
            job.ContractType,
            job.ExpirationDate,
            job.PublishedAt,
            job.Status,
            job.PublicationType,
            job.ApplicationChannel,
            job.ApplicationContact,
            job.ApplicationUrl,
            job.CompanyName,
            job.SourceName,
            job.SourceUrl,
            job.CboCode,
            job.CreatedBy,
            job.UpdatedBy,
            Location = job.Location is null ? null : new
            {
                job.Location.City,
                job.Location.State,
                job.Location.Country,
                job.Location.Type,
                job.Location.IsRemote,
                job.Location.ShowAddress
            },
            Salary = job.Salary is null ? null : new
            {
                job.Salary.Min,
                job.Salary.Max,
                job.Salary.Period,
                job.Salary.Currency,
                job.Salary.Display
            },
            Moderation = job.Moderation is null ? null : new
            {
                job.Moderation.Status,
                job.Moderation.Reason,
                job.Moderation.ModerationDate
            }
        }, SnapshotOptions);
    }
}
