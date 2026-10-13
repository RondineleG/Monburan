using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Services;

/// <summary>
/// Background service que verifica vagas expiradas a cada hora e envia notificações.
/// </summary>
public class JobExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public JobExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<JobExpirationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobExpirationBackgroundService iniciado. Verificação a cada {Interval} minutos.",
            _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar vagas expiradas no JobExpirationBackgroundService.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("JobExpirationBackgroundService encerrado.");
    }

    private async Task ProcessExpiredJobsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando verificação de vagas expiradas...");

        using var scope = _serviceProvider.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<Job>>();
        var advertiserRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<Advertiser>>();
        var notifyOnJobExpiredUseCase = scope.ServiceProvider.GetRequiredService<NotifyOnJobExpiredUseCase>();
        var notificationRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<Notification>>();

        // Buscar todas as vagas ativas usando paginação
        var allJobs = new List<Job>();
        int page = 1;
        const int pageSize = 100;
        bool hasMoreJobs = true;

        while (hasMoreJobs)
        {
            var jobsResult = await jobRepository.GetAllAsync(page, pageSize);
            if (jobsResult.Status != EResultStatus.Success)
            {
                _logger.LogError("Falha ao buscar vagas para verificação de expiração: {Error}", jobsResult.Message);
                return;
            }

            var jobs = jobsResult.Data!.ToList();
            if (jobs.Count == 0)
            {
                hasMoreJobs = false;
            }
            else
            {
                allJobs.AddRange(jobs);
                page++;

                // Se retornou menos que o tamanho da página, não há mais registros
                if (jobs.Count < pageSize)
                {
                    hasMoreJobs = false;
                }
            }
        }

        var now = DateTime.UtcNow;
        var expiredJobs = allJobs
            .Where(j => j.Status == EJobStatus.Active && j.ExpirationDate < now)
            .ToList();

        if (expiredJobs.Count == 0)
        {
            _logger.LogInformation("Nenhuma vaga expirada encontrada nesta verificação.");
            return;
        }

        _logger.LogInformation("Encontradas {Count} vaga(s) expirada(s). Processando...", expiredJobs.Count);

        int successCount = 0;
        int errorCount = 0;

        foreach (var job in expiredJobs)
        {
            try
            {
                // Atualizar status da vaga para Expired
                job.Status = EJobStatus.Expired;
                var updateResult = await jobRepository.UpdateAsync(job);

                if (updateResult.Status != EResultStatus.Success)
                {
                    _logger.LogError("Falha ao atualizar status da vaga {JobId} para Expired: {Error}",
                        job.Id.Value, updateResult.Message);
                    errorCount++;
                    continue;
                }

                // Enviar email para anunciante
                var notifyResult = await notifyOnJobExpiredUseCase.ExecuteAsync(new NotifyOnJobExpiredRequest(job.Id.Value));
                if (notifyResult.Status != EResultStatus.Success)
                {
                    _logger.LogWarning("Falha ao enviar notificação de expiração para vaga {JobId}: {Error}",
                        job.Id.Value, notifyResult.Message);
                }

                // Criar notificação no sistema para o anunciante
                var notification = new Notification
                {
                    Id = Id.Create(Guid.NewGuid()),
                    RecipientId = job.AdvertiserId.Value.ToString(),
                    Title = "Vaga Expirada",
                    Message = $"A vaga '{job.Title}' expirou e não está mais visível.",
                    Type = ENotificationType.JobExpired,
                    IsRead = false
                };

                var notificationResult = await notificationRepository.CreateAsync(notification);
                if (notificationResult.Status != EResultStatus.Success)
                {
                    _logger.LogWarning("Falha ao criar notificação para vaga expirada {JobId}: {Error}",
                        job.Id.Value, notificationResult.Message);
                }

                _logger.LogInformation("Vaga {JobId} - {JobTitle} processada com sucesso.", job.Id.Value, job.Title);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar vaga expirada {JobId}: {JobTitle}", job.Id.Value, job.Title);
                errorCount++;
            }
        }

        _logger.LogInformation("Verificação de vagas expiradas concluída. Sucessos: {Success}, Erros: {Errors}",
            successCount, errorCount);
    }
}
