using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Jobs;

public class UpdateJobUseCase : IUseCase<UpdateJobRequest, JobResponse>
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IEntityValidator<Job> _jobValidator;

    public UpdateJobUseCase(IBaseRepository<Job> jobRepository, IEntityValidator<Job> jobValidator)
    {
        _jobRepository = jobRepository;
        _jobValidator = jobValidator;
    }

    public async Task<RequestResult<JobResponse>> ExecuteAsync(UpdateJobRequest request)
    {
        var existingJobResult = await _jobRepository.GetByIdAsync(Id.Create(request.Id));
        if (existingJobResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.EntityNotFound("Job", request.Id, "Vaga não encontrada.");

        var existingJob = existingJobResult.Data!;

        existingJob.Title = request.Title;
        existingJob.Description = request.Description;
        existingJob.Requirements = request.Requirements;
        existingJob.Responsibilities = request.Responsibilities;
        existingJob.Benefits = request.Benefits;
        existingJob.ApplicationInstructions = request.ApplicationInstructions;
        existingJob.NumberOfOpenings = request.NumberOfOpenings;
        existingJob.Observations = request.Observations;
        existingJob.Category = request.Category;
        existingJob.Tags = request.Tags;
        existingJob.ExpirationDate = request.ExpirationDate;

        if (existingJob.Location != null)
        {
            existingJob.Location.City = request.City;
            existingJob.Location.State = request.State;
            existingJob.Location.Country = request.Country;
            existingJob.Location.Type = request.Type;
            existingJob.Location.IsRemote = request.IsRemote;
            existingJob.Location.ShowAddress = request.ShowAddress;
        }

        if (existingJob.Salary != null)
        {
            existingJob.Salary.Min = request.SalaryMin;
            existingJob.Salary.Max = request.SalaryMax;
            existingJob.Salary.Currency = request.SalaryCurrency;
            existingJob.Salary.Display = request.SalaryDisplay;
        }

        var validationResult = _jobValidator.ValidateUpdate(existingJob);
        if (!validationResult.IsValid)
        {
            var updateValidationResult = RequestResult<JobResponse>.WithNoContent();
            foreach (var error in validationResult.Errors)
            {
                updateValidationResult.ValidationResult.AddError(error.Message, error.Field);
            }
            return updateValidationResult;
        }

        var updateResult = await _jobRepository.UpdateAsync(existingJob);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(updateResult);

        return RequestResult<JobResponse>.Success(MapToResponse(updateResult.Data!));
    }

    private static JobResponse MapToResponse(Job job)
    {
        return new JobResponse
        {
            Id = job.Id.Value,
            Title = job.Title,
            Description = job.Description,
            Requirements = job.Requirements,
            Responsibilities = job.Responsibilities,
            Benefits = job.Benefits,
            ApplicationInstructions = job.ApplicationInstructions,
            NumberOfOpenings = job.NumberOfOpenings,
            Observations = job.Observations,
            Category = job.Category,
            Tags = job.Tags,
            ExpirationDate = job.ExpirationDate,
            Status = job.Status.ToString(),
            City = job.Location?.City,
            State = job.Location?.State,
            Country = job.Location?.Country ?? "BR",
            Type = job.Location?.Type ?? "OnSite",
            IsRemote = job.Location?.IsRemote ?? false,
            SalaryMin = job.Salary?.Min,
            SalaryMax = job.Salary?.Max,
            SalaryCurrency = job.Salary?.Currency ?? "BRL",
            SalaryDisplay = job.Salary?.Display ?? true,
            Views = job.Counters?.Views ?? 0,
            Applications = job.Counters?.Applications ?? 0,
            AdvertiserId = job.AdvertiserId.Value,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt
        };
    }
}
