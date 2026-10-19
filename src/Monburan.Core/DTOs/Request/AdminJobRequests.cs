using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Core.DTOs.Request;

public class AdminListJobsRequest
{
    public string? SearchTerm { get; set; }

    /// <summary>Nome de <see cref="EJobStatus"/> (Active, Paused, Hidden, Rejected, Expired); vazio lista todas.</summary>
    public string? Status { get; set; }

    public Guid? AdvertiserId { get; set; }

    public int Page { get; set; } = 1;

    public int Size { get; set; } = 20;
}

/// <param name="Reason">Obrigatório para ocultar e rejeitar; a empresa vê o texto.</param>
/// <param name="RenewUntil">Nova validade, exigida para ativar uma vaga cuja validade já passou.</param>
public sealed record AdminChangeJobStatusRequest(Guid JobId, EJobStatus NewStatus, string? Reason, DateTime? RenewUntil, AdminActor Actor);

/// <param name="Reason">Obrigatório; a empresa vê o texto.</param>
public sealed record AdminDeleteJobRequest(Guid JobId, string? Reason, AdminActor Actor);
