using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Notifications;

namespace Monburan.Core.UseCases.JobAlerts;

/// <summary>A vaga recém-criada a ser avaliada contra os alertas ativos.</summary>
public sealed record NotifyJobAlertsOnJobCreatedRequest(Job Job);

/// <summary>
/// Avalia os alertas ativos quando uma vaga é publicada: para cada alerta que casa, cria a notificação
/// <c>NewJob</c> ("Nova vaga para o seu alerta: &lt;nome&gt;") e atualiza <c>LastMatchedAt</c>.
/// Só roda na criação da vaga (edição não notifica); devolve quantos alertas notificaram.
/// </summary>
public class NotifyJobAlertsOnJobCreatedUseCase : IUseCase<NotifyJobAlertsOnJobCreatedRequest, int>
{
    private readonly IJobAlertRepository _alerts;
    private readonly CreateNotificationUseCase _createNotification;
    private readonly ILogger<NotifyJobAlertsOnJobCreatedUseCase> _logger;

    public NotifyJobAlertsOnJobCreatedUseCase(
        IJobAlertRepository alerts,
        CreateNotificationUseCase createNotification,
        ILogger<NotifyJobAlertsOnJobCreatedUseCase> logger)
    {
        _alerts = alerts;
        _createNotification = createNotification;
        _logger = logger;
    }

    public async Task<RequestResult<int>> ExecuteAsync(NotifyJobAlertsOnJobCreatedRequest request)
    {
        var job = request.Job;
        if (job.Status != EJobStatus.Active || job.ExpirationDate < DateTime.UtcNow)
            return RequestResult<int>.Success(0);

        var alertsResult = await _alerts.ListActiveAsync();
        if (alertsResult.Status != EResultStatus.Success || alertsResult.Data is null)
            return RequestResult<int>.FromResult(alertsResult);

        var notified = 0;
        foreach (var alert in alertsResult.Data)
        {
            // Um alerta problemático não impede os próximos de serem avaliados
            try
            {
                if (!JobAlertMatcher.Matches(job, alert))
                    continue;

                var notificationResult = await _createNotification.ExecuteAsync(new CreateNotificationRequest
                {
                    Title = "Nova vaga para o seu alerta",
                    Message = $"Nova vaga para o seu alerta: {alert.Name}",
                    RecipientId = alert.CandidateId.Value.ToString(),
                    Type = ENotificationType.NewJob
                });
                if (notificationResult.Status != EResultStatus.Success)
                {
                    _logger.LogWarning("Alerta {AlertId} casou com a vaga {JobId}, mas a notificação falhou: {Detail}",
                        alert.Id.Value, job.Id.Value, notificationResult.Message);
                    continue;
                }

                alert.LastMatchedAt = DateTime.UtcNow;
                var updateResult = await _alerts.UpdateAsync(alert);
                if (updateResult.Status != EResultStatus.Success)
                {
                    _logger.LogWarning("Alerta {AlertId} notificado, mas o LastMatchedAt não foi atualizado: {Detail}",
                        alert.Id.Value, updateResult.Message);
                }

                notified++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao avaliar o alerta {AlertId} para a vaga {JobId}", alert.Id.Value, job.Id.Value);
            }
        }

        return RequestResult<int>.Success(notified);
    }
}
