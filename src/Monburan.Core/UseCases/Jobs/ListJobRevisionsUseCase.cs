using System.Text.Json.Nodes;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

/// <summary>Histórico imutável da vaga para o anunciante dono, com autoria traduzida por papel.</summary>
public sealed class ListJobRevisionsUseCase : IUseCase<ListJobRevisionsRequest, IReadOnlyList<JobRevisionResponse>>
{
    public const string JobHistoryFeature = "historico-vaga";

    private readonly IJobRepository _jobs;

    public ListJobRevisionsUseCase(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<RequestResult<IReadOnlyList<JobRevisionResponse>>> ExecuteAsync(ListJobRevisionsRequest request, CancellationToken cancellationToken = default)
    {
        var jobResult = await _jobs.GetByIdAsync(Id.Create(request.JobId), cancellationToken);
        if (jobResult.Status != EResultStatus.Success || jobResult.Data is null)
            return RequestResult<IReadOnlyList<JobRevisionResponse>>.FromResult(jobResult);

        var job = jobResult.Data;
        if (job.AdvertiserId?.Value != request.AdvertiserId)
            return RequestResult<IReadOnlyList<JobRevisionResponse>>.WithDomainError("Vaga pertence a outro anunciante.");

        var revisions = await _jobs.ListRevisionsAsync(job.Id, cancellationToken);
        var advertiserId = job.AdvertiserId.Value.ToString();
        return RequestResult<IReadOnlyList<JobRevisionResponse>>.Success(revisions
            .Select(revision => new JobRevisionResponse(
                revision.Id.Value,
                revision.Revision,
                revision.Action.ToString(),
                revision.Status.ToString(),
                ActorKind(revision, advertiserId),
                revision.CreatedAt,
                SanitizeSnapshot(revision.SnapshotJson)))
            .ToList());
    }

    private static string ActorKind(JobRevision revision, string advertiserId)
    {
        if (revision.ActorId == advertiserId)
            return "Advertiser";
        if (revision.ActorId.StartsWith("system:", StringComparison.Ordinal))
            return "System";
        return "Admin";
    }

    private static string SanitizeSnapshot(string snapshotJson)
    {
        if (JsonNode.Parse(snapshotJson) is not JsonObject snapshot)
            return snapshotJson;

        snapshot.Remove("CreatedBy");
        snapshot.Remove("UpdatedBy");
        return snapshot.ToJsonString();
    }
}

public sealed record ListJobRevisionsRequest(Guid JobId, Guid AdvertiserId);
