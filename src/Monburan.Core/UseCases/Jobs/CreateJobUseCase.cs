using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class CreateJobUseCase : IUseCase<CreateJobRequest, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IEntityValidator<Job> _jobValidator;
    private readonly CheckJobLimitUseCase _checkJobLimit;
    private readonly NotifyJobAlertsOnJobCreatedUseCase _notifyAlerts;
    private readonly ILogger<CreateJobUseCase> _logger;

    public CreateJobUseCase(
        IBaseRepository<Job> jobRepository,
        IEntityValidator<Job> jobValidator,
        CheckJobLimitUseCase checkJobLimit,
        NotifyJobAlertsOnJobCreatedUseCase notifyAlerts,
        ILogger<CreateJobUseCase> logger)
    {
        _jobRepository = jobRepository;
        _jobValidator = jobValidator;
        _checkJobLimit = checkJobLimit;
        _notifyAlerts = notifyAlerts;
        _logger = logger;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(CreateJobRequest request)
    {
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(request.AdvertiserId),
            Title = request.Title,
            Description = request.Description,
            Requirements = request.Requirements,
            Responsibilities = request.Responsibilities,
            Benefits = request.Benefits,
            ApplicationInstructions = request.ApplicationInstructions,
            NumberOfOpenings = request.NumberOfOpenings,
            Observations = request.Observations,
            Category = request.Category,
            Tags = request.Tags,
            ContractType = request.ContractType,
            ExpirationDate = request.ExpirationDate,
            Status = EJobStatus.Active,
            TenantId = "default",
            Location = new JobLocation
            {
                City = request.City,
                State = request.State,
                Country = request.Country,
                Type = request.Type,
                IsRemote = request.IsRemote,
                ShowAddress = request.ShowAddress
            },
            Salary = new SalaryRange
            {
                Min = request.SalaryMin,
                Max = request.SalaryMax,
                Period = request.SalaryPeriod,
                Currency = request.SalaryCurrency,
                Display = request.SalaryDisplay
            },
            Moderation = new ModerationInfo { Status = EModerationStatus.Pending },
            Boost = new JobBoost { IsActive = false },
            Highlight = new JobHighlight { IsActive = false },
            Counters = new JobCounters()
        };

        var validationResult = _jobValidator.Validate(job);
        if (!validationResult.IsValid)
        {
            var createResult = RequestResult<JobResponse>.WithNoContent();
            foreach (var error in validationResult.Errors)
            {
                createResult.ValidationResult.AddError(error.Message, error.Field);
            }
            return createResult;
        }

        // Limite de vagas ativas do plano — a página de planos promete esse limite
        var limitResult = await _checkJobLimit.ExecuteAsync(new CheckJobLimitRequest(request.AdvertiserId));
        if (limitResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(limitResult);

        if (!limitResult.Data!.CanCreate)
        {
            return RequestResult<JobResponse>.WithDomainError(
                $"Seu plano {limitResult.Data.PlanName} permite até {limitResult.Data.MaxJobs} vagas ativas. " +
                "Encerre uma vaga ou mude de plano para publicar outra.");
        }

        var createJobResult = await _jobRepository.CreateAsync(job);
        if (createJobResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(createJobResult);

        // Alertas de vaga (não bloqueante: avaliar alertas nunca impede a publicação)
        try
        {
            var notifyResult = await _notifyAlerts.ExecuteAsync(new NotifyJobAlertsOnJobCreatedRequest(createJobResult.Data!));
            if (notifyResult.Status != EResultStatus.Success)
                _logger.LogWarning("Vaga {JobId} criada, mas os alertas não foram avaliados: {Detail}",
                    createJobResult.Data!.Id.Value, notifyResult.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao avaliar os alertas da vaga {JobId}", createJobResult.Data!.Id.Value);
        }

        return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(createJobResult.Data!));
    }
}
