using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Notifications;

/// <summary>
/// Avisa o anunciante quando uma candidatura é criada: notificação no app/site e e-mail.
/// </summary>
public sealed class NotifyOnApplicationCreatedUseCase : IUseCase<NotifyOnApplicationCreatedRequest, bool>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IBaseRepository<Advertiser> _advertiserRepository;
    private readonly SendEmailNotificationUseCase _sendEmailUseCase;
    private readonly CreateNotificationUseCase _createNotificationUseCase;
    private readonly ILogger<NotifyOnApplicationCreatedUseCase> _logger;

    public NotifyOnApplicationCreatedUseCase(
        IBaseRepository<Application> applicationRepository,
        IBaseRepository<Job> jobRepository,
        IBaseRepository<Advertiser> advertiserRepository,
        SendEmailNotificationUseCase sendEmailUseCase,
        CreateNotificationUseCase createNotificationUseCase,
        ILogger<NotifyOnApplicationCreatedUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _advertiserRepository = advertiserRepository;
        _sendEmailUseCase = sendEmailUseCase;
        _createNotificationUseCase = createNotificationUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(NotifyOnApplicationCreatedRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Buscar candidatura
            var applicationResult = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId), cancellationToken);
            if (applicationResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Candidatura não encontrada: {ApplicationId}", request.ApplicationId);
                return RequestResult<bool>.WithError("Candidatura não encontrada.");
            }

            var application = applicationResult.Data!;

            // Buscar vaga para obter dados do anunciante
            var jobResult = await _jobRepository.GetByIdAsync(application.JobId, cancellationToken);
            if (jobResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Vaga não encontrada: {JobId}", application.JobId.Value);
                return RequestResult<bool>.WithError("Vaga não encontrada.");
            }

            var job = jobResult.Data!;

            if (job.AdvertiserId is null)
                return RequestResult<bool>.Success(true); // vaga externa: não há anunciante para avisar

            // Buscar anunciante para obter email
            var advertiserResult = await _advertiserRepository.GetByIdAsync(job.AdvertiserId, cancellationToken);
            if (advertiserResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Anunciante não encontrado: {AdvertiserId}", job.AdvertiserId.Value);
                return RequestResult<bool>.WithError("Anunciante não encontrado.");
            }

            var advertiser = advertiserResult.Data!;

            // Notificação in-app independe do e-mail (que pode não estar configurado)
            await CreateInAppNotificationAsync(advertiser, job, application);

            // Preparar template de email
            var (subject, html) = EmailTemplates.ApplicationReceivedEmail(advertiser.Name, job.Title);

            // Enviar email
            var emailRequest = new EmailNotificationRequest
            {
                ToEmail = advertiser.Email,
                Subject = subject,
                HtmlContent = html
            };

            var emailResult = await _sendEmailUseCase.ExecuteAsync(emailRequest, cancellationToken);

            if (emailResult.Status != EResultStatus.Success)
            {
                _logger.LogError("Falha ao enviar email de candidatura para {AdvertiserEmail}: {Error}",
                    advertiser.Email, emailResult.Message);
                return RequestResult<bool>.WithError(emailResult.Message);
            }

            if (emailResult.Data)
            {
                _logger.LogInformation("Email de candidatura enviado com sucesso para {AdvertiserEmail} - Vaga: {JobTitle}",
                    advertiser.Email, job.Title);
            }
            else
            {
                _logger.LogWarning("Email de candidatura não entregue para {AdvertiserEmail} - Vaga: {JobTitle}",
                    advertiser.Email, job.Title);
            }

            return RequestResult<bool>.Success(emailResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar notificação de candidatura criada: {ApplicationId}",
                request.ApplicationId);
            return RequestResult<bool>.WithError($"Erro ao enviar notificação: {ex.Message}");
        }
    }

    private async Task CreateInAppNotificationAsync(Advertiser advertiser, Job job, Application application, CancellationToken cancellationToken = default)
    {
        try
        {
            var candidateName = string.IsNullOrWhiteSpace(application.Candidate?.Name) ? "Um candidato" : application.Candidate.Name;
            var result = await _createNotificationUseCase.ExecuteAsync(new CreateNotificationRequest
            {
                RecipientId = advertiser.Id.Value.ToString(),
                Type = ENotificationType.ApplicationStatus,
                Title = "Nova candidatura",
                Message = $"{candidateName} se candidatou à vaga \"{job.Title}\"."
            },
            cancellationToken);

            if (result.Status != EResultStatus.Success)
                _logger.LogWarning("Notificação in-app de nova candidatura não criada: {Detail}", result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar notificação in-app de nova candidatura {ApplicationId}", application.Id.Value);
        }
    }
}

public record NotifyOnApplicationCreatedRequest(Guid ApplicationId);
