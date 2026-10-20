using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>Alertas de vaga do candidato autenticado. O CandidateId sempre vem da claim, nunca do body.</summary>
public static class JobAlertEndpoints
{
    public static RouteGroupBuilder MapJobAlertEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/job-alerts").WithTags("Job alerts")
            .RequireAuthorization("ApiBearer");

        group.MapGet("/", async (
            ICurrentUserHelper currentUser,
            ListJobAlertsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Lista os alertas de vaga do candidato autenticado");

        group.MapPost("/", async (
            JobAlertRequest request,
            ICurrentUserHelper currentUser,
            CreateJobAlertUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new CreateJobAlertCommand(candidateId.Value, request), cancellationToken);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithSummary("Cria alerta de vaga (409 quando o candidato já tem 10)");

        group.MapPut("/{id:guid}", async (
            Guid id,
            JobAlertRequest request,
            ICurrentUserHelper currentUser,
            UpdateJobAlertUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new UpdateJobAlertCommand(candidateId.Value, id, request), cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza um alerta do candidato autenticado (o de outro candidato não é encontrado)");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICurrentUserHelper currentUser,
            DeleteJobAlertUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new DeleteJobAlertCommand(candidateId.Value, id), cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithSummary("Exclui um alerta do candidato autenticado");

        return api;
    }
}
