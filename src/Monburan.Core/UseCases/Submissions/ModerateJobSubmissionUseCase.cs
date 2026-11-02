using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Submissions;

/// <summary>
/// Decisão humana sobre uma sugestão. Só a aprovação cria vaga, e ela é criada como externa, com a empresa e a fonte da
/// sugestão e o canal que o moderador confirmou. Toda decisão fica no histórico e na trilha de auditoria.
/// </summary>
public sealed class ModerateJobSubmissionUseCase : IUseCase<ModerateJobSubmissionRequest, Guid>
{
    private readonly IJobSubmissionRepository _submissions;
    private readonly IJobRepository _jobs;
    private readonly IEntityValidator<Job> _jobValidator;
    private readonly IAdminAuditLog _audit;

    public ModerateJobSubmissionUseCase(
        IJobSubmissionRepository submissions,
        IJobRepository jobs,
        IEntityValidator<Job> jobValidator,
        IAdminAuditLog audit)
    {
        _submissions = submissions;
        _jobs = jobs;
        _jobValidator = jobValidator;
        _audit = audit;
    }

    public async Task<RequestResult<Guid>> ExecuteAsync(ModerateJobSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Actor is null || string.IsNullOrWhiteSpace(request.Actor.Id))
            return RequestResult<Guid>.WithDomainError("Somente um moderador identificado pode decidir uma sugestão.");

        var found = await _submissions.GetByIdAsync(Id.Create(request.SubmissionId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<Guid>.FromResult(found);

        var submission = found.Data;
        var target = JobSubmissionRules.StatusFor(request.Decision);
        if (!JobSubmissionRules.CanTransition(submission.Status, target))
            return RequestResult<Guid>.WithDomainError("Esta sugestão já foi decidida e não pode ser alterada.");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        Job? publishedJob = null;

        switch (request.Decision)
        {
            case ESubmissionDecision.Reject:
            case ESubmissionDecision.NeedsMoreInformation:
                if (note is null)
                    return RequestResult<Guid>.WithValidationError(
                        request.Decision == ESubmissionDecision.Reject ? "Informe o motivo da rejeição." : "Descreva a informação que falta.",
                        "Note");
                break;

            case ESubmissionDecision.Duplicate:
                var duplicates = (request.DuplicateOfJobId is not null ? 1 : 0) + (request.DuplicateOfSubmissionId is not null ? 1 : 0);
                if (duplicates != 1)
                    return RequestResult<Guid>.WithValidationError("Indique a vaga publicada ou a sugestão que esta repete.", "DuplicateOf");
                if (request.DuplicateOfSubmissionId == submission.Id.Value)
                    return RequestResult<Guid>.WithValidationError("A sugestão não pode repetir a si mesma.", "DuplicateOf");
                break;

            case ESubmissionDecision.Approve:
                var built = BuildJob(submission, request);
                if (built.Error is not null)
                    return built.Error;
                publishedJob = built.Job;
                break;
        }

        if (publishedJob is not null)
        {
            var created = await _jobs.CreateAsync(publishedJob, cancellationToken);
            if (created.Status != EResultStatus.Success)
                return RequestResult<Guid>.FromResult(created);
            submission.ApprovedJobId = publishedJob.Id;
        }

        if (request.Decision == ESubmissionDecision.Duplicate)
        {
            submission.DuplicateOfJobId = request.DuplicateOfJobId is { } jobId ? Id.Create(jobId) : null;
            submission.DuplicateOfSubmissionId = request.DuplicateOfSubmissionId is { } otherId ? Id.Create(otherId) : null;
        }

        submission.Status = target;
        submission.ModerationNote = note;
        submission.ModeratedBy = request.Actor.Id;
        submission.ModeratedAt = DateTime.UtcNow;
        submission.History.Add(new JobSubmissionHistory
        {
            Id = Id.Create(Guid.NewGuid()),
            SubmissionId = submission.Id,
            Status = target,
            ActorId = request.Actor.Id,
            Note = note,
            TenantId = "default"
        });

        var updated = await _submissions.UpdateAsync(submission, cancellationToken);
        if (updated.Status != EResultStatus.Success)
        {
            // Sem a decisão gravada, a vaga criada não pode ficar publicada sem sugestão aprovada: desfaz a criação
            if (publishedJob is not null)
                await _jobs.DeleteAsync(publishedJob.Id, cancellationToken);
            return RequestResult<Guid>.FromResult(updated);
        }

        await _audit.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.SubmissionDecided,
            request.Actor,
            AdminAuditActions.SubmissionTarget,
            submission.Id.Value.ToString(),
            submission.Title,
            $"{target}{(note is null ? string.Empty : ". Observação: " + note)}"),
            cancellationToken: cancellationToken);

        return RequestResult<Guid>.Success(submission.Id.Value);
    }

    private (Job? Job, RequestResult<Guid>? Error) BuildJob(JobSubmission submission, ModerateJobSubmissionRequest request)
    {
        if (request.Channel is null)
            return (null, RequestResult<Guid>.WithValidationError("Escolha como a candidatura é feita (site, WhatsApp, e-mail ou presencial).", "Channel"));
        if (request.ExpirationDate is null)
            return (null, RequestResult<Guid>.WithValidationError("Informe o prazo de candidatura.", "ExpirationDate"));

        var hasSalary = request.SalaryMin is not null || request.SalaryMax is not null || request.SalaryPeriod is not null;
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = null,
            PublicationType = EPublicationType.External,
            Title = submission.Title,
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? submission.CompanyName : request.CompanyName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? submission.Description ?? string.Empty : request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            ExpirationDate = request.ExpirationDate.Value,
            Status = EJobStatus.Active,
            ApplicationChannel = request.Channel.Value,
            ApplicationUrl = Clean(request.ApplicationUrl),
            ApplicationContact = Clean(request.ApplicationContact),
            ApplicationInstructions = Clean(request.ApplicationInstructions),
            CboCode = Clean(request.CboCode),
            SourceName = submission.SourceDescription ?? submission.SourceUrl,
            SourceUrl = submission.SourceUrl,
            LastVerifiedAt = DateTime.UtcNow,
            Location = new JobLocation
            {
                City = submission.City,
                State = submission.State,
                Country = "BR",
                Type = "OnSite",
                IsRemote = false,
                ShowAddress = false
            },
            Salary = hasSalary ? new SalaryRange
            {
                Min = request.SalaryMin,
                Max = request.SalaryMax,
                Period = request.SalaryPeriod,
                Currency = "BRL",
                Display = true
            } : null,
            TenantId = "default"
        };

        var validation = _jobValidator.Validate(job);
        if (!validation.IsValid)
        {
            var first = validation.Errors.First();
            return (null, RequestResult<Guid>.WithValidationError(first.Message, first.Field ?? string.Empty));
        }

        return (job, null);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
