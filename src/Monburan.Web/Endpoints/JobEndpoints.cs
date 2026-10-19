using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/jobs").WithTags("Jobs");

        // Público — lista com filtros (parity com a página /jobs do site)
        group.MapGet("/", async (
            SearchJobsUseCase useCase,
            CancellationToken cancellationToken,
            int? page = null, int? size = null,
            string? searchTerm = null, string? city = null, string? state = null,
            bool? isRemote = null, string? type = null,
            decimal? salaryMin = null, decimal? salaryMax = null,
            string? category = null, string? sortBy = null, bool? sortDescending = null) =>
        {
            var result = await useCase.ExecuteAsync(BuildRequest(
                page, size, searchTerm, city, state, isRemote, type, salaryMin, salaryMax,
                category, sortBy, sortDescending, advertiserId: null));
            if (result.Data is not null)
            {
                foreach (var job in result.Data.Jobs)
                    HideUndisclosedSalary(job);
            }

            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithSummary("Lista vagas com filtros e paginação");

        // Bearer — vagas do anunciante logado (antes de /{id} por especificidade)
        group.MapGet("/mine", async (
            ICurrentUserHelper currentUser,
            SearchJobsUseCase useCase,
            CancellationToken cancellationToken,
            int? page = null, int? size = null,
            string? searchTerm = null, string? city = null, string? state = null,
            bool? isRemote = null, string? type = null,
            decimal? salaryMin = null, decimal? salaryMax = null,
            string? category = null, string? sortBy = null, bool? sortDescending = null) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var result = await useCase.ExecuteAsync(BuildRequest(
                page, size, searchTerm, city, state, isRemote, type, salaryMin, salaryMax,
                category, sortBy, sortDescending, advertiserId));
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Lista vagas do anunciante autenticado");

        // Público — detalhe
        group.MapGet("/{id:guid}", async (
            Guid id,
            GetJobByIdUseCase useCase,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(id);

            // Vaga pausada, oculta ou rejeitada só aparece para a empresa dona (que manda o token); para o resto do mundo
            // é como se não existisse. Sem isso o conteúdo (e o motivo da moderação) vazaria para quem soubesse o id.
            if (result.Data is { } job && job.Status is not ("Active" or "Expired"))
            {
                var auth = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                var ownerId = auth.Succeeded ? auth.Principal?.GetAdvertiserId() : null;
                if (ownerId != job.AdvertiserId)
                    return RequestResult<JobResponse>.EntityNotFound("Job", id, "Vaga não encontrada.").ToHttpResult();
            }

            if (result.Data is not null)
                HideUndisclosedSalary(result.Data);

            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithSummary("Detalhe de uma vaga (pausada, oculta ou rejeitada: só a empresa dona)");

        // Bearer — criar vaga (AdvertiserId sempre vem da claim, nunca do body)
        group.MapPost("/", async (
            CreateJobRequest request,
            ICurrentUserHelper currentUser,
            CreateJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            request.AdvertiserId = advertiserId.Value;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Cria vaga para o anunciante autenticado");

        // Bearer — atualizar vaga (com checagem de propriedade)
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateJobRequest request,
            ICurrentUserHelper currentUser,
            GetJobByIdUseCase getJob,
            UpdateJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var existing = await getJob.ExecuteAsync(id);
            if (existing.Status != Core.Enums.EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.AdvertiserId != advertiserId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Vaga pertence a outro anunciante");

            request.Id = id;
            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Atualiza vaga do anunciante autenticado");

        // Bearer — remover vaga (com checagem de propriedade)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICurrentUserHelper currentUser,
            GetJobByIdUseCase getJob,
            DeleteJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var existing = await getJob.ExecuteAsync(id);
            if (existing.Status != Core.Enums.EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.AdvertiserId != advertiserId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Vaga pertence a outro anunciante");

            var result = await useCase.ExecuteAsync(id);
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Remove vaga do anunciante autenticado");

        return api;
    }

    // Endpoints públicos: o anunciante pode ocultar a faixa salarial — o valor não deve
    // sair na resposta (antes só o site respeitava SalaryDisplay; o app exibia o salário)
    private static void HideUndisclosedSalary(Core.DTOs.Response.JobResponse job)
    {
        if (job.SalaryDisplay)
            return;

        job.SalaryMin = null;
        job.SalaryMax = null;
    }

    private static ListJobsRequest BuildRequest(
        int? page, int? size, string? searchTerm, string? city, string? state,
        bool? isRemote, string? type, decimal? salaryMin, decimal? salaryMax,
        string? category, string? sortBy, bool? sortDescending, Guid? advertiserId)
    {
        return new ListJobsRequest
        {
            Page = page is > 0 ? page.Value : 1,
            Size = size is > 0 ? Math.Min(size.Value, 100) : 20,
            SearchTerm = searchTerm,
            City = city,
            State = state,
            IsRemote = isRemote,
            Type = type,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            Category = category,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "CreatedAt" : sortBy,
            SortDescending = sortDescending ?? true,
            AdvertiserId = advertiserId
        };
    }
}
