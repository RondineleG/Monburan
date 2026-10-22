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
            ICurrentUserHelper currentUser,
            HttpContext httpContext,
            CancellationToken cancellationToken,
            int? page = null, int? size = null,
            string? searchTerm = null, string? city = null, string? state = null,
            bool? isRemote = null, string? type = null,
            decimal? salaryMin = null, decimal? salaryMax = null,
            string? category = null, string? sortBy = null, bool? sortDescending = null,
            string? contractType = null, string? salaryPeriod = null, int? publishedWithinDays = null) =>
        {
            var (candidateId, _) = await ResolveApiIdentityAsync(httpContext, currentUser);
            var result = await useCase.ExecuteAsync(BuildRequest(
                page, size, searchTerm, city, state, isRemote, type, salaryMin, salaryMax,
                category, sortBy, sortDescending, advertiserId: null, candidateId,
                contractType, salaryPeriod, publishedWithinDays), cancellationToken);
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
                category, sortBy, sortDescending, advertiserId, candidateId: null), cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Lista vagas do anunciante autenticado");

        // Bearer — recomendações para o candidato autenticado
        group.MapGet("/recommended", async (
            ICurrentUserHelper currentUser,
            ListRecommendedJobsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
            if (result.Data is not null)
            {
                foreach (var job in result.Data.Jobs)
                    HideUndisclosedSalary(job);
            }

            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Lista vagas recomendadas para o candidato autenticado");

        // Bearer — vagas salvas do candidato
        group.MapGet("/saved", async (
            ICurrentUserHelper currentUser,
            ListSavedJobsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(candidateId.Value, cancellationToken);
            if (result.Data is not null)
            {
                foreach (var job in result.Data.Jobs)
                    HideUndisclosedSalary(job);
            }

            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Lista vagas salvas do candidato autenticado");

        // Bearer — salvar vaga (idempotente)
        group.MapPut("/{id:guid}/save", async (
            Guid id,
            ICurrentUserHelper currentUser,
            SaveJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new SaveJobRequest(candidateId.Value, id), cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Salva a vaga para o candidato autenticado (idempotente)");

        // Bearer — remover vaga salva (idempotente)
        group.MapDelete("/{id:guid}/save", async (
            Guid id,
            ICurrentUserHelper currentUser,
            UnsaveJobUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var candidateId = currentUser.GetCandidateId();
            if (candidateId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var result = await useCase.ExecuteAsync(new UnsaveJobRequest(candidateId.Value, id), cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Remove a vaga salva do candidato autenticado (idempotente)");

        // Público — registrar visualização (a visita do próprio anunciante não conta)
        group.MapPost("/{id:guid}/views", async (
            Guid id,
            ICurrentUserHelper currentUser,
            HttpContext httpContext,
            RegisterJobViewUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var (_, advertiserId) = await ResolveApiIdentityAsync(httpContext, currentUser);
            var result = await useCase.ExecuteAsync(new RegisterJobViewRequest(id, advertiserId), cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .AllowAnonymous()
        .WithSummary("Registra uma visualização da vaga (a do próprio anunciante não conta)");

        // Público — detalhe
        group.MapGet("/{id:guid}", async (
            Guid id,
            GetJobByIdUseCase useCase,
            ICurrentUserHelper currentUser,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var (candidateId, advertiserId) = await ResolveApiIdentityAsync(httpContext, currentUser);
            var result = await useCase.ExecuteAsync(id, candidateId, cancellationToken);

            // Vaga pausada, oculta ou rejeitada só aparece para a empresa dona (que manda o token); para o resto do mundo
            // é como se não existisse. Sem isso o conteúdo (e o motivo da moderação) vazaria para quem soubesse o id.
            if (result.Data is { } job && job.Status is not ("Active" or "Expired"))
            {
                if (advertiserId != job.AdvertiserId)
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
            var result = await useCase.ExecuteAsync(request, cancellationToken);
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

            var existing = await getJob.ExecuteAsync(id, cancellationToken);
            if (existing.Status != Core.Enums.EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.AdvertiserId != advertiserId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Vaga pertence a outro anunciante");

            request.Id = id;
            var result = await useCase.ExecuteAsync(request, cancellationToken);
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

            var existing = await getJob.ExecuteAsync(id, cancellationToken);
            if (existing.Status != Core.Enums.EResultStatus.Success || existing.Data is null)
                return existing.ToHttpResult();

            if (existing.Data.AdvertiserId != advertiserId.Value)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Vaga pertence a outro anunciante");

            var result = await useCase.ExecuteAsync(id, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Remove vaga do anunciante autenticado");

        return api;
    }

    // O bearer não é o scheme padrão da aplicação (o site usa cookie): em rota anônima, só a autenticação
    // explícita do JWT devolve quem está chamando — sem isso o app mobile nunca veria IsSaved nem o dono
    // da vaga seria reconhecido na contagem de visualizações.
    private static async Task<(Guid? CandidateId, Guid? AdvertiserId)> ResolveApiIdentityAsync(
        HttpContext httpContext, ICurrentUserHelper currentUser)
    {
        if (currentUser.IsAuthenticated())
            return (currentUser.GetCandidateId(), currentUser.GetAdvertiserId());

        var auth = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        return auth.Succeeded
            ? (auth.Principal?.GetCandidateId(), auth.Principal?.GetAdvertiserId())
            : (null, null);
    }

    // Endpoints públicos: o anunciante pode ocultar a faixa salarial — o valor não deve
    // sair na resposta (antes só o site respeitava SalaryDisplay; o app exibia o salário)
    private static void HideUndisclosedSalary(Core.DTOs.Response.JobResponse job)    {
        if (job.SalaryDisplay)
            return;

        job.SalaryMin = null;
        job.SalaryMax = null;
    }

    private static ListJobsRequest BuildRequest(
        int? page, int? size, string? searchTerm, string? city, string? state,
        bool? isRemote, string? type, decimal? salaryMin, decimal? salaryMax,
        string? category, string? sortBy, bool? sortDescending, Guid? advertiserId, Guid? candidateId,
        string? contractType = null, string? salaryPeriod = null, int? publishedWithinDays = null)
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
            AdvertiserId = advertiserId,
            CandidateId = candidateId,
            ContractType = contractType,
            SalaryPeriod = salaryPeriod,
            PublishedWithinDays = publishedWithinDays is > 0 ? publishedWithinDays : null
        };
    }
}
