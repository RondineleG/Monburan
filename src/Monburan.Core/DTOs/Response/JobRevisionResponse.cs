namespace Monburan.Core.DTOs.Response;

/// <summary>Revisão imutável da vaga exposta ao dono, sem identificadores internos do autor.</summary>
public sealed record JobRevisionResponse(
    Guid Id,
    int Revision,
    string Action,
    string Status,
    string ActorKind,
    DateTime CreatedAt,
    string SnapshotJson);
