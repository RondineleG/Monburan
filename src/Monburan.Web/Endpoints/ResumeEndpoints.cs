using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Resumes;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class ResumeEndpoints
{
    public static RouteGroupBuilder MapResumeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/resumes").WithTags("Resumes")
            .RequireAuthorization("ApiBearer");

        group.MapGet("/mine", async (
            ICurrentUserHelper currentUser,
            ListResumesByCandidateUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value);
            return result.ToHttpResult();
        })
        .WithSummary("Lista currículos do candidato autenticado");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ICurrentUserHelper currentUser,
            GetResumeByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(id);
            if (result.Status != EResultStatus.Success || result.Data is null)
                return result.ToHttpResult();

            if (result.Data.CandidateId != candidateId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Currículo pertence a outro candidato");

            return result.ToHttpResult();
        })
        .WithSummary("Detalhe de um currículo do candidato autenticado");

        // CandidateId sempre da claim (IDOR-safe)
        group.MapPost("/", async (
            CreateResumeRequest request,
            ICurrentUserHelper currentUser,
            CreateResumeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            request.CandidateId = candidateId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithSummary("Cria currículo para o candidato autenticado");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateResumeRequest request,
            ICurrentUserHelper currentUser,
            GetResumeByIdUseCase getResume,
            UpdateResumeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var existing = await getResume.ExecuteAsync(id);
            if (existing.Status != EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.CandidateId != candidateId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Currículo pertence a outro candidato");

            request.Id = id;
            request.CandidateId = candidateId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza currículo do candidato autenticado");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICurrentUserHelper currentUser,
            GetResumeByIdUseCase getResume,
            DeleteResumeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var existing = await getResume.ExecuteAsync(id);
            if (existing.Status != EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.CandidateId != candidateId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Currículo pertence a outro candidato");

            var result = await useCase.ExecuteAsync(id);
            return result.ToHttpResult();
        })
        .WithSummary("Remove currículo do candidato autenticado");

        // O app e o site só oferecem o recurso quando a IA está configurada neste ambiente
        group.MapGet("/improvements/availability", (IAiTextClient ai) =>
            Results.Ok(new { enabled = ai.IsConfigured, provider = ai.IsConfigured ? ai.ProviderName : null }))
        .WithSummary("Informa se as sugestões de melhoria com IA estão ativas e qual provedor recebe o conteúdo");

        // CandidateId sempre da claim (IDOR-safe); o conteúdo só sai com autorização expressa no corpo
        group.MapPost("/{id:guid}/improvements", async (
            Guid id,
            ImproveResumeRequest request,
            ICurrentUserHelper currentUser,
            ImproveResumeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            request.ResumeId = id;
            request.CandidateId = candidateId.Value;
            var result = await useCase.ExecuteAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Gera sugestões de melhoria do currículo com IA (exige acceptsExternalProcessing: true)");

        return api;
    }
}
