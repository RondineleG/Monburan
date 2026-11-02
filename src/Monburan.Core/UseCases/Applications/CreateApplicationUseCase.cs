using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class CreateApplicationUseCase : IUseCase<CreateApplicationRequest, ApplicationResponse>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IBaseRepository<Resume> _resumeRepository;
    private readonly IApplicationStatusHistoryRepository _historyRepository;
    private readonly NotifyOnApplicationCreatedUseCase _notifyOnCreatedUseCase;
    private readonly ILogger<CreateApplicationUseCase> _logger;

    public CreateApplicationUseCase(
        IBaseRepository<Application> applicationRepository,
        IJobRepository jobRepository,
        IBaseRepository<Resume> resumeRepository,
        IApplicationStatusHistoryRepository historyRepository,
        NotifyOnApplicationCreatedUseCase notifyOnCreatedUseCase,
        ILogger<CreateApplicationUseCase> logger)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _resumeRepository = resumeRepository;
        _historyRepository = historyRepository;
        _notifyOnCreatedUseCase = notifyOnCreatedUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<ApplicationResponse>> ExecuteAsync(CreateApplicationRequest request, CancellationToken cancellationToken = default)
    {
        // Verificar se vaga existe e está ativa
        var jobResult = await _jobRepository.GetByIdAsync(Id.Create(request.JobId), cancellationToken);
        if (jobResult.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var job = jobResult.Data!;
        if (job.Status != EJobStatus.Active)
            return RequestResult<ApplicationResponse>.WithDomainError("Vaga não está mais ativa.");

        // Vaga externa recebe candidatura no canal da fonte (site, WhatsApp, e-mail ou presencial), nunca na Monburan
        if (job.PublicationType == EPublicationType.External)
            return RequestResult<ApplicationResponse>.WithDomainError("Esta vaga recebe candidaturas fora da Monburan. Use o canal indicado na vaga.");

        // Currículo anexado deve existir e pertencer ao candidato
        if (request.ResumeId.HasValue)
        {
            var resumeResult = await _resumeRepository.GetByIdAsync(Id.Create(request.ResumeId.Value), cancellationToken);
            if (resumeResult.Status != EResultStatus.Success || resumeResult.Data is null
                || resumeResult.Data.CandidateId.Value != request.CandidateId)
            {
                return RequestResult<ApplicationResponse>.WithValidationError(
                    "Currículo não encontrado.", nameof(request.ResumeId));
            }
        }

        // Criar candidatura
        var application = new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(request.CandidateId),
            JobId = Id.Create(request.JobId),
            Status = EApplicationStatus.Sent,
            AppliedAt = DateTime.UtcNow,
            Notes = request.Notes,
            ResumeId = request.ResumeId,
            TenantId = "default"
        };

        var result = await _applicationRepository.CreateAsync(application, cancellationToken);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.FromResult(result);

        // Histórico de etapas (não bloqueante: a candidatura existe mesmo se o histórico falhar)
        await TryWriteHistoryAsync(application, note: null, responsible: null);

        // Incrementar contador de aplicações da vaga — de forma atômica. Contar no código
        // (ler a vaga, somar, gravar a vaga inteira) perdia a contagem quando duas
        // candidaturas entravam ao mesmo tempo e ainda sobrescrevia a moderação da vaga.
        await _jobRepository.IncrementApplicationsAsync(request.JobId, cancellationToken);

        // Enviar notificação por email para o anunciante (não bloqueante)
        try
        {
            var notify = await _notifyOnCreatedUseCase.ExecuteAsync(new NotifyOnApplicationCreatedRequest(result.Data!.Id.Value), cancellationToken);
            if (notify.Status != EResultStatus.Success)
                _logger.LogWarning("Candidatura {ApplicationId} criada, mas o anunciante não foi notificado: {Detail}",
                    result.Data.Id.Value, notify.Message);
        }
        catch (Exception ex)
        {
            // Falha na notificação não impede a candidatura, mas precisa ficar visível no log
            _logger.LogError(ex, "Falha ao notificar o anunciante sobre a candidatura {ApplicationId}", result.Data!.Id.Value);
        }

        return RequestResult<ApplicationResponse>.Success(MapToResponse(result.Data!));
    }

    // Falha na gravação do histórico fica no log: a trilha é um extra e não pode derrubar a candidatura
    private async Task TryWriteHistoryAsync(Application application, string? note, string? responsible, CancellationToken cancellationToken = default)
    {
        try
        {
            var historyResult = await _historyRepository.CreateAsync(new ApplicationStatusHistory
            {
                Id = Id.Create(Guid.NewGuid()),
                ApplicationId = application.Id,
                Status = application.Status,
                Date = DateTime.UtcNow,
                Note = note,
                Responsible = responsible,
                TenantId = application.TenantId
            },
            cancellationToken);

            if (historyResult.Status != EResultStatus.Success)
                _logger.LogWarning("Histórico da candidatura {ApplicationId} não foi gravado: {Detail}",
                    application.Id.Value, historyResult.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar o histórico da candidatura {ApplicationId}", application.Id.Value);
        }
    }

    private static ApplicationResponse MapToResponse(Application app) => new()
    {
        Id = app.Id.Value,
        CandidateId = app.CandidateId.Value,
        JobId = app.JobId.Value,
        Status = app.Status.ToString(),
        AppliedAt = app.AppliedAt,
        ResumeId = app.ResumeId,
        ViewedAt = app.ViewedAt,
        SelectedAt = app.SelectedAt,
        RejectedAt = app.RejectedAt,
        Notes = app.Notes,
        CreatedAt = app.CreatedAt
    };
}
