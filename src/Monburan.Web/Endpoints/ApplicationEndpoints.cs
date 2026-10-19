using Monburan.Core.DTOs.Request;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.UseCases.Jobs;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplicationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/applications").WithTags("Applications")
            .RequireAuthorization("ApiBearer");

        // IDOR-safe: CandidateId sempre da claim, nunca do body
        group.MapPost("/", async (
            CreateApplicationRequest request,
            ICurrentUserHelper currentUser,
            CreateApplicationUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            request.CandidateId = candidateId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithSummary("Candidata o usuário autenticado a uma vaga");

        group.MapGet("/mine", async (
            int? page, int? size,
            ICurrentUserHelper currentUser,
            ListApplicationsByCandidateUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new ListApplicationsByCandidateRequest
            {
                CandidateId = candidateId.Value,
                Page = page is > 0 ? page.Value : 1,
                Size = size is > 0 ? Math.Min(size.Value, IApplicationRepository.MaxListSize) : 0
            });
            return result.ToHttpResult();
        })
        .WithSummary("Lista candidaturas do candidato autenticado");

        group.MapGet("/{id:guid}/history", async (
            Guid id,
            ICurrentUserHelper currentUser,
            ListApplicationHistoryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(new ListApplicationHistoryRequest(
                id, currentUser.GetCandidateId(), currentUser.GetAdvertiserId()));
            return result.ToHttpResult();
        })
        .WithSummary("Histórico de etapas da candidatura (candidato dono ou anunciante dono da vaga)");

        // Atualiza status — AdvertiserId sempre da claim
        group.MapPut("/{id:guid}/status", async (
            Guid id,
            UpdateApplicationStatusRequest request,
            ICurrentUserHelper currentUser,
            UpdateApplicationStatusUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            request.ApplicationId = id;
            request.AdvertiserId = advertiserId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza status de uma candidatura (anunciante dono da vaga)");

        // GET /api/v1/jobs/{jobId}/applications — candidaturas recebidas na vaga
        api.MapGet("/jobs/{jobId:guid}/applications", async (
            Guid jobId,
            ICurrentUserHelper currentUser,
            GetJobByIdUseCase getJob,
            ListApplicationsByJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var job = await getJob.ExecuteAsync(jobId);
            if (job.Status != Core.Enums.EResultStatus.Success || job.Data is null)
                return job.ToHttpResult();

            if (job.Data.AdvertiserId != advertiserId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Vaga pertence a outro anunciante");

            var result = await useCase.ExecuteAsync(new ListApplicationsByJobRequest
            {
                AdvertiserId = advertiserId.Value,
                JobId = jobId
            });
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithTags("Applications")
        .WithSummary("Lista candidaturas recebidas em uma vaga do anunciante");

        return api;
    }
}
