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
/// UseCase para enviar email ao candidato quando o status da candidatura muda.
/// </summary>
public sealed class NotifyOnApplicationStatusChangedUseCase : IUseCase<NotifyOnApplicationStatusChangedRequest, bool>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IBaseRepository<Candidate> _candidateRepository;
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly SendEmailNotificationUseCase _sendEmailUseCase;
    private readonly CreateNotificationUseCase _createNotificationUseCase;
    private readonly ILogger<NotifyOnApplicationStatusChangedUseCase> _logger;

    public NotifyOnApplicationStatusChangedUseCase(
        IBaseRepository<Application> applicationRepository,
        IBaseRepository<Candidate> candidateRepository,
        IBaseRepository<Job> jobRepository,
        SendEmailNotificationUseCase sendEmailUseCase,
        CreateNotificationUseCase createNotificationUseCase,
        ILogger<NotifyOnApplicationStatusChangedUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _candidateRepository = candidateRepository;
        _jobRepository = jobRepository;
        _sendEmailUseCase = sendEmailUseCase;
        _createNotificationUseCase = createNotificationUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(NotifyOnApplicationStatusChangedRequest request)
    {
        try
        {
            // Buscar candidatura
            var applicationResult = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId));
            if (applicationResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Candidatura não encontrada: {ApplicationId}", request.ApplicationId);
                return RequestResult<bool>.WithError("Candidatura não encontrada.");
            }

            var application = applicationResult.Data!;

            // Buscar candidato para obter email
            var candidateResult = await _candidateRepository.GetByIdAsync(application.CandidateId);
            if (candidateResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Candidato não encontrado: {CandidateId}", application.CandidateId.Value);
                return RequestResult<bool>.WithError("Candidato não encontrado.");
            }

            var candidate = candidateResult.Data!;

            // Buscar vaga para obter título
            var jobResult = await _jobRepository.GetByIdAsync(application.JobId);
            if (jobResult.Status != EResultStatus.Success)
            {
                _logger.LogWarning("Vaga não encontrada: {JobId}", application.JobId.Value);
                return RequestResult<bool>.WithError("Vaga não encontrada.");
            }

            var job = jobResult.Data!;

            // Notificação in-app independe do e-mail (que pode não estar configurado)
            await CreateInAppNotificationAsync(candidate, job, application.Status);

            // Preparar template de email — status em pt-BR (antes ia "Selected"/"Rejected")
            var statusText = StatusLabel(application.Status);
            var (subject, html) = EmailTemplates.ApplicationStatusChangedEmail(candidate.Name, job.Title, statusText);

            // Enviar email
            var emailRequest = new EmailNotificationRequest
            {
                ToEmail = candidate.Email,
                Subject = subject,
                HtmlContent = html
            };

            var emailResult = await _sendEmailUseCase.ExecuteAsync(emailRequest);

            if (emailResult.Status != EResultStatus.Success)
            {
                _logger.LogError("Falha ao enviar email de status para {CandidateEmail}: {Error}",
                    candidate.Email, emailResult.Message);
                return RequestResult<bool>.WithError(emailResult.Message);
            }

            if (emailResult.Data)
            {
                _logger.LogInformation("Email de status enviado com sucesso para {CandidateEmail} - Vaga: {JobTitle} - Status: {Status}",
                    candidate.Email, job.Title, statusText);
            }
            else
            {
                _logger.LogWarning("Email de status não entregue para {CandidateEmail} - Vaga: {JobTitle} - Status: {Status}",
                    candidate.Email, job.Title, statusText);
            }

            return RequestResult<bool>.Success(emailResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar notificação de status mudado: {ApplicationId}",
                request.ApplicationId);
            return RequestResult<bool>.WithError($"Erro ao enviar notificação: {ex.Message}");
        }
    }

    public static string StatusLabel(EApplicationStatus status) => status switch
    {
        EApplicationStatus.Sent => "Enviada",
        EApplicationStatus.Viewed => "Visualizada pela empresa",
        EApplicationStatus.Selected => "Selecionada",
        EApplicationStatus.Rejected => "Não selecionada",
        EApplicationStatus.Archived => "Arquivada",
        _ => "Em análise"
    };

    private async Task CreateInAppNotificationAsync(Candidate candidate, Job job, EApplicationStatus status)
    {
        var (title, message) = status switch
        {
            EApplicationStatus.Selected => ("Você foi selecionado(a)!", $"Sua candidatura para a vaga \"{job.Title}\" foi selecionada. A empresa deve entrar em contato."),
            EApplicationStatus.Rejected => ("Atualização da sua candidatura", $"Sua candidatura para a vaga \"{job.Title}\" não seguiu no processo. Continue buscando novas oportunidades."),
            EApplicationStatus.Viewed => ("Candidatura visualizada", $"A empresa visualizou sua candidatura para a vaga \"{job.Title}\"."),
            _ => ("Atualização da sua candidatura", $"Sua candidatura para a vaga \"{job.Title}\" agora está: {StatusLabel(status)}.")
        };

        try
        {
            var result = await _createNotificationUseCase.ExecuteAsync(new CreateNotificationRequest
            {
                RecipientId = candidate.Id.Value.ToString(),
                Type = ENotificationType.ApplicationStatus,
                Title = title,
                Message = message
            });

            if (result.Status != EResultStatus.Success)
                _logger.LogWarning("Notificação in-app de status não criada: {Detail}", result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar notificação in-app de status para o candidato {CandidateId}", candidate.Id.Value);
        }
    }
}

public record NotifyOnApplicationStatusChangedRequest(Guid ApplicationId);
