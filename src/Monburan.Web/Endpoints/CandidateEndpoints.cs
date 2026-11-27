using System.Security.Claims;

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

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
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
            var result = await useCase.ExecuteAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Atualiza o perfil do candidato autenticado");

        group.MapGet("/me/career-profile", async (
            ICurrentUserHelper currentUser,
            GetCareerProfileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Perfil de carreira do candidato (200 com campos nulos quando ainda não existe)");

        group.MapPut("/me/career-profile", async (
            CareerProfileRequest request,
            ICurrentUserHelper currentUser,
            UpsertCareerProfileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new UpsertCareerProfileCommand(candidateId.Value, request), cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Cria ou atualiza o perfil de carreira do candidato autenticado");

        group.MapGet("/me/international-preferences", async (
            ICurrentUserHelper currentUser,
            ClaimsPrincipal user,
            GetInternationalPreferencesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new GetInternationalPreferencesCommand(candidateId.Value), user, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireFeature("carreira-internacional")
        .WithSummary("Preferências internacionais do candidato (MI-01 — feature flag)");

        group.MapPut("/me/international-preferences", async (
            InternationalPreferencesRequest request,
            ICurrentUserHelper currentUser,
            ClaimsPrincipal user,
            UpsertInternationalPreferencesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new UpsertInternationalPreferencesCommand(candidateId.Value, request), user, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireFeature("carreira-internacional")
        .WithSummary("Cria ou atualiza preferências internacionais (países, mobilidade, autorização autodeclarada)");

        group.MapGet("/me/languages", async (
            ICurrentUserHelper currentUser,
            ClaimsPrincipal user,
            GetCandidateLanguagesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new GetCandidateLanguagesCommand(candidateId.Value), user, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireFeature("carreira-internacional")
        .WithSummary("Idiomas autodeclarados do candidato (MI-01 — feature flag)");

        group.MapPut("/me/languages", async (
            CandidateLanguagesRequest request,
            ICurrentUserHelper currentUser,
            ClaimsPrincipal user,
            ReplaceCandidateLanguagesUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new ReplaceCandidateLanguagesCommand(candidateId.Value, request), user, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireFeature("carreira-internacional")
        .WithSummary("Substitui o conjunto de idiomas autodeclarados do candidato");

        group.MapGet("/me/progress", async (
            ICurrentUserHelper currentUser,
            GetCandidateProgressUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .WithSummary("Progresso do candidato: completude, candidaturas por situação, vagas salvas e alertas ativos");

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

            var result = await useCase.ExecuteAsync(advertiserId.Value, cancellationToken);
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
            var result = await useCase.ExecuteAsync(request, cancellationToken);
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
                }, title: "Erro de validação");
            }

            await using var stream = file.OpenReadStream();
            var upload = await uploadUseCase.ExecuteAsync(new UploadFileRequest
            {
                FileStream = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileType = EFileType.Image
            }, cancellationToken);

            if (upload.Status != EResultStatus.Success || upload.Data is null)
                return upload.ToHttpResult();

            // Só a logo muda — o upload não depende do restante do perfil estar completo
            var updateResult = await updateLogo.ExecuteAsync(
                new UpdateAdvertiserLogoRequest(advertiserId.Value, upload.Data.FileUrl), cancellationToken);

            return updateResult.ToHttpResult();
        })
        .DisableAntiforgery()
        .WithSummary("Upload da logo e atualização do perfil do anunciante autenticado");

        return api;
    }
}
