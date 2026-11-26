using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Integrations;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Jobs;
using Monburan.Web.Data;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Tokens opacos e consulta consentida de integrações externas. A rota pública de vagas continua
/// anônima; aqui o bearer mbi_ prova vínculo pessoal, titular mapeado e escopo antes da leitura.
/// </summary>
public static class IntegrationEndpoints
{
    private const string Feature = "integracao-himarshall";

    public static RouteGroupBuilder MapIntegrationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/integrations").WithTags("Integrations");

        group.MapGet("/tokens", async (
            HttpContext http,
            IntegrationTokenService tokens,
            ICurrentUserHelper currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.GetCandidateId() is null || FeatureFlagService.UserIdOf(http.User) is not { } userId)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var rows = await tokens.ListActiveAsync(userId, cancellationToken);
            return Results.Ok(rows.Select(token => ToResponse(token, includeToken: false)));
        })
        .RequireAuthorization("ApiBearer")
        .RequireFeature(Feature)
        .WithSummary("Lista tokens de integração do titular, sem segredos");

        group.MapPost("/tokens", async (
            CreateIntegrationTokenRequest request,
            HttpContext http,
            IntegrationTokenService tokens,
            ICurrentUserHelper currentUser,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.GetCandidateId() is not { } candidateId || FeatureFlagService.UserIdOf(http.User) is not { } userId)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de candidato necessário");

            var (token, secret, error) = await tokens.CreateAsync(
                userId,
                candidateId,
                request.Name,
                request.Scopes,
                request.ExpiresInDays,
                http.User.Identity?.Name ?? userId.ToString("D"),
                cancellationToken);
            return token is null
                ? Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: error)
                : Results.Ok(ToResponse(token, includeToken: true, secret));
        })
        .RequireAuthorization("ApiBearer")
        .RequireFeature(Feature)
        .WithSummary("Emite token opaco de integração; o valor aparece só nesta resposta");

        group.MapDelete("/tokens/{id:guid}", async (
            Guid id,
            HttpContext http,
            IntegrationTokenService tokens,
            CancellationToken cancellationToken) =>
        {
            if (FeatureFlagService.UserIdOf(http.User) is not { } userId)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Conta inválida");

            return await tokens.RevokeAsync(id, userId, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization("ApiBearer")
        .WithSummary("Revoga token de integração do titular");

        group.MapGet("/jobs", async (
            HttpContext http,
            ApplicationDbContext context,
            IntegrationTokenService tokens,
            IFeatureFlags flags,
            SearchJobsUseCase useCase,
            CancellationToken cancellationToken,
            int? page = null,
            int? size = null,
            string? searchTerm = null,
            string? city = null,
            string? state = null,
            bool? isRemote = null) =>
        {
            var header = http.Request.Headers.Authorization.ToString();
            var secret = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
            var token = await tokens.ValidateAsync(secret, IntegrationTokenService.JobsScope, cancellationToken);
            if (token is null)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Token de integração inválido, expirado ou revogado");

            var holderActive = await context.Candidates.AsNoTracking()
                    .AnyAsync(candidate => candidate.Id.Value == token.CandidateId && !candidate.IsDeleted, cancellationToken)
                && await context.Users.AsNoTracking()
                    .AnyAsync(user => user.Id == token.UserId && user.IsActive, cancellationToken);
            if (!holderActive)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Titular do vínculo não possui conta e perfil de candidato ativos");

            var principal = await IntegrationPrincipalAsync(context, token.UserId, cancellationToken);
            if (!await flags.IsEnabledAsync(Feature, principal, cancellationToken))
                return Results.NotFound();

            await tokens.MarkUsedAsync(token, cancellationToken);

            var result = await useCase.ExecuteAsync(new ListJobsRequest
            {
                Page = Math.Max(page ?? 1, 1),
                Size = Math.Clamp(size ?? 20, 1, 50),
                SearchTerm = searchTerm,
                City = city,
                State = state,
                IsRemote = isRemote,
                CandidateId = token.CandidateId
            }, cancellationToken);
            if (result.Data is null)
                return result.ToHttpResult();

            var response = new ListIntegrationJobsResponse
            {
                Jobs = [.. result.Data.Jobs.Select(job => new IntegrationJobResponse
                {
                    Id = job.Id,
                    Title = job.Title,
                    Company = job.CompanyName ?? job.AdvertiserName ?? "Empresa não informada",
                    Location = job.IsRemote ? "Remoto" : Location(job.City, job.State),
                    ApplyUrl = !string.IsNullOrWhiteSpace(job.ApplicationUrl)
                        ? job.ApplicationUrl!
                        : $"{http.Request.Scheme}://{http.Request.Host}/jobs/{job.Id:D}"
                })],
                TotalCount = result.Data.TotalCount,
                Page = result.Data.Page,
                Size = result.Data.Size
            };
            return Results.Ok(response);
        })
        .AllowAnonymous()
        .WithSummary("Consulta vagas públicas mínimas com vínculo pessoal e escopo jobs:read");

        return group;
    }

    private static async Task<ClaimsPrincipal> IntegrationPrincipalAsync(
        ApplicationDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var roleNames = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name != null
            select role.Name!).ToArrayAsync(cancellationToken);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new("user_type", "candidate")
        };
        claims.AddRange(roleNames.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "IntegrationToken"));
    }

    private static IntegrationTokenResponse ToResponse(IntegrationToken token, bool includeToken, string? secret = null) =>
        new()
        {
            Id = token.Id,
            Name = token.Name,
            Scopes = token.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            RevokedAt = token.RevokedAt,
            LastUsedAt = token.LastUsedAt,
            Token = includeToken ? secret : null
        };

    private static string? Location(string? city, string? state) =>
        string.Join('/', new[] { city, state }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } value ? value : null;
}
