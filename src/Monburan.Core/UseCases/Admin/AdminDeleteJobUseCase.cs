using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Admin;

/// <summary>Exclusão (lógica) de uma vaga de qualquer empresa, com motivo obrigatório, aviso à empresa e registro na auditoria.</summary>
public sealed class AdminDeleteJobUseCase : IUseCase<AdminDeleteJobRequest, bool>
{
    private readonly IBaseRepository<Job> _jobs;
    private readonly CreateNotificationUseCase _notifications;
    private readonly IAdminAuditLog _audit;

    public AdminDeleteJobUseCase(IBaseRepository<Job> jobs, CreateNotificationUseCase notifications, IAdminAuditLog audit)
    {
        _jobs = jobs;
        _notifications = notifications;
        _audit = audit;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(AdminDeleteJobRequest request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (reason is null || reason.Length < AdminChangeJobStatusUseCase.ReasonMinLength)
        {
            return RequestResult<bool>.WithValidationError(
                $"Informe o motivo (pelo menos {AdminChangeJobStatusUseCase.ReasonMinLength} caracteres): a empresa vai vê-lo.", nameof(request.Reason));
        }

        if (reason.Length > AdminChangeJobStatusUseCase.ReasonMaxLength)
        {
            return RequestResult<bool>.WithValidationError(
                $"O motivo deve ter no máximo {AdminChangeJobStatusUseCase.ReasonMaxLength} caracteres.", nameof(request.Reason));
        }

        var found = await _jobs.GetByIdAsync(Id.Create(request.JobId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<bool>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var job = found.Data;
        var deleted = await _jobs.DeleteAsync(job.Id, cancellationToken);
        if (deleted.Status != EResultStatus.Success)
            return RequestResult<bool>.FromResult(deleted);

        await _notifications.ExecuteAsync(new CreateNotificationRequest
        {
            RecipientId = job.AdvertiserId.Value.ToString(),
            Title = "Sua vaga foi removida pela moderação",
            Message = $"A vaga \"{job.Title}\" foi removida da plataforma. Motivo: {reason}.",
            Type = ENotificationType.System
        },
        cancellationToken);

        await _audit.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.JobDeleted, request.Actor, AdminAuditActions.JobTarget, job.Id.Value.ToString(), job.Title, $"Motivo: {reason}"),
            cancellationToken);

        return RequestResult<bool>.Success(true);
    }
}
