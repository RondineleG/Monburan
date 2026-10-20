using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Mappers;
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

    public async Task<RequestResult<JobResponse>> ExecuteAsync(UpdateJobRequest request, CancellationToken cancellationToken = default)
    {
        var existingJobResult = await _jobRepository.GetByIdAsync(Id.Create(request.Id), cancellationToken);
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
        existingJob.ContractType = request.ContractType;
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
            existingJob.Salary.Period = request.SalaryPeriod;
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

        var updateResult = await _jobRepository.UpdateAsync(existingJob, cancellationToken);
        if (updateResult.Status != EResultStatus.Success)
            return RequestResult<JobResponse>.FromResult(updateResult);

        // A resposta sai da vaga carregada e editada, e não do objeto devolvido pela gravação: o
        // repositório pode devolver uma instância nova, sem as navegações carregadas, e a empresa
        // desapareceria só na edição — a divergência que o detalhe e a listagem não têm.
        return RequestResult<JobResponse>.Success(JobResponseMapper.ToResponse(existingJob));
    }
}

