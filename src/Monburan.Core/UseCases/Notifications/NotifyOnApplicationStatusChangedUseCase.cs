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
    private readonly ILogger<NotifyOnApplicationStatusChangedUseCase> _logger;

    public NotifyOnApplicationStatusChangedUseCase(
        IBaseRepository<Application> applicationRepository,
        IBaseRepository<Candidate> candidateRepository,
        IBaseRepository<Job> jobRepository,
        SendEmailNotificationUseCase sendEmailUseCase,
        ILogger<NotifyOnApplicationStatusChangedUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _candidateRepository = candidateRepository;
        _jobRepository = jobRepository;
        _sendEmailUseCase = sendEmailUseCase;
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

            // Preparar template de email
            var statusText = application.Status.ToString();
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
}

public record NotifyOnApplicationStatusChangedRequest(Guid ApplicationId);
