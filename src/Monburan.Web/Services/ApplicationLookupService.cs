using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;

namespace Monburan.Web.Services;

/// <summary>
/// Consulta prévia de candidatura: a tela pergunta se a pessoa já se candidatou à vaga
/// ANTES de oferecer o formulário. Falha de consulta nunca vira "não se candidatou" —
/// o chamador decide como reagir (tentar de novo ou link de verificação).
/// </summary>
public sealed class ApplicationLookupService(IApplicationRepository applications, ICurrentUserHelper user, ILogger<ApplicationLookupService> logger)
{
    /// <summary>
    /// A candidatura do candidato logado na vaga, ou nulo quando não há candidato
    /// logado / nenhuma candidatura. Erros de banco propagam no Status.
    /// </summary>
    public async Task<RequestResult<ApplicationResponse?>> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var candidateId = user.GetCandidateId();
        if (candidateId is null)
            return RequestResult<ApplicationResponse?>.Success(null);

        RequestResult<Monburan.Core.Entities.Application?> result;
        try
        {
            result = await applications.FindByCandidateAndJobAsync(candidateId.Value, jobId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao verificar candidatura na vaga {JobId}", jobId);
            return RequestResult<ApplicationResponse?>.WithError("Não foi possível verificar sua candidatura.");
        }
        if (result.Status != EResultStatus.Success)
            return RequestResult<ApplicationResponse?>.FromResult(result);

        var application = result.Data;
        if (application is null)
            return RequestResult<ApplicationResponse?>.Success(null);

        return RequestResult<ApplicationResponse?>.Success(new ApplicationResponse
        {
            Id = application.Id.Value,
            CandidateId = candidateId.Value,
            JobId = jobId,
            Status = application.Status.ToString(),
            AppliedAt = application.AppliedAt,
            ResumeId = application.ResumeId,
            CreatedAt = application.CreatedAt
        });
    }
}
