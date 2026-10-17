using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.UseCases.Advertisers;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.UseCases.Storage;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class CandidateEndpoints
{
    public static RouteGroupBuilder MapCandidateEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/candidates").WithTags("Candidates")
            .RequireAuthorization("ApiBearer");

        group.MapGet("/me", async (
            ICurrentUserHelper currentUser,
            GetCandidateByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value);
            return result.ToHttpResult();
        })
        .WithSummary("Perfil do candidato autenticado");

        group.MapPut("/me", async (
            UpdateCandidateRequest request,
            ICurrentUserHelper currentUser,
            UpdateCandidateUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            request.Id = candidateId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza o perfil do candidato autenticado");

        return api;
    }

    public static RouteGroupBuilder MapAdvertiserEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/advertisers").WithTags("Advertisers")
            .RequireAuthorization("ApiBearer");

        group.MapGet("/me", async (
            ICurrentUserHelper currentUser,
            GetAdvertiserByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var result = await useCase.ExecuteAsync(advertiserId.Value);
            return result.ToHttpResult();
        })
        .WithSummary("Perfil do anunciante autenticado");

        group.MapPut("/me", async (
            UpdateAdvertiserRequest request,
            ICurrentUserHelper currentUser,
            UpdateAdvertiserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            request.Id = advertiserId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza o perfil do anunciante autenticado");

        // Logo: upload + atualização do perfil em um passo (fica em /advertisers, não /files)
        group.MapPost("/logo", async (
            IFormFile file,
            ICurrentUserHelper currentUser,
            UploadFileUseCase uploadUseCase,
            UpdateAdvertiserLogoUseCase updateLogo,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            if (file is null || file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Arquivo é obrigatório"]
                });
            }

            await using var stream = file.OpenReadStream();
            var upload = await uploadUseCase.ExecuteAsync(new UploadFileRequest
            {
                FileStream = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileType = EFileType.Image
            });

            if (upload.Status != EResultStatus.Success || upload.Data is null)
                return upload.ToHttpResult();

            // Só a logo muda — o upload não depende do restante do perfil estar completo
            var updateResult = await updateLogo.ExecuteAsync(
                new UpdateAdvertiserLogoRequest(advertiserId.Value, upload.Data.FileUrl));

            return updateResult.ToHttpResult();
        })
        .DisableAntiforgery()
        .WithSummary("Upload da logo e atualização do perfil do anunciante autenticado");

        return api;
    }
}
