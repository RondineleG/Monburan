using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public class CreateApplicationUseCase : IUseCase<CreateApplicationRequest, ApplicationResponse>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IBaseRepository<Resume> _resumeRepository;
    private readonly NotifyOnApplicationCreatedUseCase _notifyOnCreatedUseCase;

    public CreateApplicationUseCase(
        IBaseRepository<Application> applicationRepository,
        IBaseRepository<Job> jobRepository,
        IBaseRepository<Resume> resumeRepository,
        NotifyOnApplicationCreatedUseCase notifyOnCreatedUseCase)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _resumeRepository = resumeRepository;
        _notifyOnCreatedUseCase = notifyOnCreatedUseCase;
    }

    public async Task<RequestResult<ApplicationResponse>> ExecuteAsync(CreateApplicationRequest request)
    {
        // Verificar se vaga existe e está ativa
        var jobResult = await _jobRepository.GetByIdAsync(Id.Create(request.JobId));
        if (jobResult.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.EntityNotFound("Job", request.JobId, "Vaga não encontrada.");

        var job = jobResult.Data!;
        if (job.Status != EJobStatus.Active)
            return RequestResult<ApplicationResponse>.WithDomainError("Vaga não está mais ativa.");

        // Currículo anexado deve existir e pertencer ao candidato
        if (request.ResumeId.HasValue)
        {
            var resumeResult = await _resumeRepository.GetByIdAsync(Id.Create(request.ResumeId.Value));
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

        var result = await _applicationRepository.CreateAsync(application);
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse>.FromResult(result);

        // Incrementar contador de aplicações da vaga
        if (job.Counters != null)
        {
            job.Counters.Applications++;
            await _jobRepository.UpdateAsync(job);
        }

        // Enviar notificação por email para o anunciante (não bloqueante)
        try
        {
            await _notifyOnCreatedUseCase.ExecuteAsync(new NotifyOnApplicationCreatedRequest(result.Data!.Id.Value));
        }
        catch (Exception)
        {
            // Falha na notificação não deve impedir a criação da candidatura
        }

        return RequestResult<ApplicationResponse>.Success(MapToResponse(result.Data!));
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
