using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Admin;

/// <summary>
/// Moderação de uma vaga de qualquer empresa: ativar, pausar, ocultar ou reprovar. Ocultar e reprovar exigem um motivo,
/// que a empresa vê (na vaga e em um aviso). Ativar uma vaga de validade vencida exige uma nova validade, e a vaga de uma
/// empresa com a conta inativa só volta depois de a conta ser reativada. Toda mudança é auditada.
/// </summary>
public sealed class AdminChangeJobStatusUseCase : IUseCase<AdminChangeJobStatusRequest, AdminJobResponse>
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;
    private const int MaxRenewalDays = 365;

    private static readonly EJobStatus[] Allowed = [EJobStatus.Active, EJobStatus.Paused, EJobStatus.Hidden, EJobStatus.Rejected];

    private readonly IJobRepository _jobs;
    private readonly IBaseRepository<Advertiser> _advertisers;
    private readonly CreateNotificationUseCase _notifications;
    private readonly IAdminAuditLog _audit;
    private readonly TimeProvider _time;

    public AdminChangeJobStatusUseCase(
        IJobRepository jobs,
        IBaseRepository<Advertiser> advertisers,
        CreateNotificationUseCase notifications,
        IAdminAuditLog audit,
        TimeProvider time)
    {
        _jobs = jobs;
        _advertisers = advertisers;
        _notifications = notifications;
        _audit = audit;
        _time = time;
    }

    public async Task<RequestResult<AdminJobResponse>> ExecuteAsync(AdminChangeJobStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!Allowed.Contains(request.NewStatus))
            return RequestResult<AdminJobResponse>.WithValidationError("Situação inválida para a administração.", nameof(request.NewStatus));

        var reason = request.Reason?.Trim();
        if (request.NewStatus is EJobStatus.Hidden or EJobStatus.Rejected && (reason is null || reason.Length < ReasonMinLength))
        {
            return RequestResult<AdminJobResponse>.WithValidationError(
                $"Informe o motivo (pelo menos {ReasonMinLength} caracteres): a empresa vai vê-lo.", nameof(request.Reason));
        }

        if (reason is { Length: > ReasonMaxLength })
            return RequestResult<AdminJobResponse>.WithValidationError($"O motivo deve ter no máximo {ReasonMaxLength} caracteres.", nameof(request.Reason));

        var found = await _jobs.GetByIdAsync(Id.Create(request.JobId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<AdminJobResponse>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var job = found.Data;
        var now = _time.GetUtcNow().UtcDateTime;
        var today = now.Date;
        var previous = job.Status;

        if (previous == request.NewStatus)
            return RequestResult<AdminJobResponse>.WithDomainError($"A vaga já está {StatusLabel(previous).ToLowerInvariant()}.");

        if (request.NewStatus == EJobStatus.Active)
        {
            var advertiser = await _advertisers.GetByIdAsync(job.AdvertiserId, cancellationToken);
            if (advertiser.Data?.Status == EAdvertiserStatus.Blocked)
                return RequestResult<AdminJobResponse>.WithDomainError("A conta da empresa está inativa: reative a conta antes de ativar as vagas dela.");

            if (job.ExpirationDate.Date < today)
            {
                if (request.RenewUntil is not { } until || until.Date <= today || until.Date > today.AddDays(MaxRenewalDays))
                {
                    return RequestResult<AdminJobResponse>.WithDomainError(
                        "A validade da vaga já passou. Informe até quando renová-la (no máximo 1 ano) para ativá-la.");
                }

                job.ExpirationDate = DateTime.SpecifyKind(until.Date, DateTimeKind.Utc);
            }
        }

        job.Moderation ??= new ModerationInfo();
        job.Moderation.ModeratorId = request.Actor.Id;
        job.Moderation.ModerationDate = now;
        switch (request.NewStatus)
        {
            case EJobStatus.Active:
                job.Moderation.Status = EModerationStatus.Approved;
                job.Moderation.Reason = null;
                break;
            case EJobStatus.Hidden or EJobStatus.Rejected:
                job.Moderation.Status = EModerationStatus.Rejected;
                job.Moderation.Reason = reason;
                break;
            default:
                job.Moderation.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                break;
        }

        job.Status = request.NewStatus;

        // Status, validade e moderação vão numa gravação só: se uma parte falhasse sozinha, a vaga
        // ficaria rejeitada e ativa ao mesmo tempo.
        var updated = await _jobs.UpdateModerationAsync(job, cancellationToken);
        if (updated.Status != EResultStatus.Success)
            return RequestResult<AdminJobResponse>.FromResult(updated);

        await NotifyAsync(job, request.NewStatus, reason);
        await _audit.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.JobStatusChanged, request.Actor, AdminAuditActions.JobTarget, job.Id.Value.ToString(), job.Title,
            $"{StatusLabel(previous)} → {StatusLabel(request.NewStatus)}" + (string.IsNullOrWhiteSpace(reason) ? string.Empty : $". Motivo: {reason}")),
            cancellationToken: cancellationToken);

        return RequestResult<AdminJobResponse>.Success(AdminJobMapper.ToResponse(job, today));
    }

    /// <summary>Aviso dentro da plataforma para a empresa. Se falhar, a moderação já foi feita e vale.</summary>
    private async Task NotifyAsync(Job job, EJobStatus status, string? reason, CancellationToken cancellationToken = default)
    {
        if (job.AdvertiserId is null)
            return; // vaga externa: não há anunciante para avisar

        var (title, message) = status switch
        {
            EJobStatus.Hidden => ("Sua vaga foi ocultada pela moderação",
                $"A vaga \"{job.Title}\" saiu do ar. Motivo: {reason}. Ajuste o que for preciso e fale com o suporte para revisá-la."),
            EJobStatus.Rejected => ("Sua vaga foi reprovada pela moderação",
                $"A vaga \"{job.Title}\" não cumpre as regras da plataforma. Motivo: {reason}."),
            EJobStatus.Paused => ("Sua vaga foi pausada pela administração",
                $"A vaga \"{job.Title}\" parou de receber candidaturas." + (string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Motivo: {reason}.")),
            _ => ("Sua vaga foi reativada pela administração",
                $"A vaga \"{job.Title}\" voltou a receber candidaturas até {job.ExpirationDate:dd/MM/yyyy}.")
        };

        await _notifications.ExecuteAsync(new CreateNotificationRequest
        {
            RecipientId = job.AdvertiserId.Value.ToString(),
            Title = title,
            Message = message,
            Type = ENotificationType.System
        },
        cancellationToken);
    }

    internal static string StatusLabel(EJobStatus status) => status switch
    {
        EJobStatus.Active => "Ativa",
        EJobStatus.Paused => "Pausada",
        EJobStatus.Hidden => "Oculta",
        EJobStatus.Rejected => "Reprovada",
        EJobStatus.Expired => "Expirada",
        _ => status.ToString()
    };
}
