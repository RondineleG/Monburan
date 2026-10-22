using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Services;

/// <summary>
/// Leitura autorizada de currículo: o dono gerencia o dele e a empresa lê apenas o
/// currículo que veio anexado numa candidatura para uma vaga dela. Quem não tem
/// permissão recebe "não encontrado", igual a um id inexistente (sem confirmar que existe).
/// </summary>
public sealed class ResumeViewerService(
    GetResumeByIdUseCase getResume,
    IApplicationRepository applications,
    IBaseRepository<Candidate> candidates,
    ICurrentUserHelper user,
    ILogger<ResumeViewerService> logger)
{
    public async Task<RequestResult<ResumeViewerResponse>> GetAsync(Guid resumeId, CancellationToken cancellationToken = default)
    {
        RequestResult<ResumeViewerResponse> Guarded() => RequestResult<ResumeViewerResponse>.WithError("Falha ao carregar currículo.");
        RequestResult<Monburan.Core.DTOs.Response.ResumeResponse> found;
        try
        {
            found = await getResume.ExecuteAsync(resumeId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao carregar currículo {ResumeId}", resumeId);
            return Guarded();
        }
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<ResumeViewerResponse>.FromResult(found);

        var resume = found.Data;

        var candidateId = user.GetCandidateId();
        var isOwner = candidateId.HasValue && resume.CandidateId == candidateId.Value;

        var allowed = isOwner;
        var advertiserId = user.GetAdvertiserId();
        if (!allowed && advertiserId.HasValue)
        {
            try
            {
                allowed = await applications.CanAdvertiserViewResumeAsync(
                    advertiserId.Value, resumeId, resume.CandidateId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao verificar acesso ao currículo {ResumeId}", resumeId);
                return Guarded();
            }
        }

        if (!allowed)
            return RequestResult<ResumeViewerResponse>.EntityNotFound("Resume", resumeId, "Currículo não encontrado.");

        // O contato só sai do cadastro DEPOIS da autorização confirmada
        RequestResult<Candidate> contact;
        try
        {
            contact = await candidates.GetByIdAsync(Id.Create(resume.CandidateId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao carregar candidato do currículo {ResumeId}", resumeId);
            return Guarded();
        }
        if (contact.Status != EResultStatus.Success)
            return RequestResult<ResumeViewerResponse>.FromResult(contact);

        return RequestResult<ResumeViewerResponse>.Success(new ResumeViewerResponse
        {
            Resume = resume,
            IsOwner = isOwner,
            Name = contact.Data?.Name,
            Email = contact.Data?.Email,
            Phone = contact.Data?.Phone,
            LinkedIn = contact.Data?.LinkedIn
        });
    }
}
