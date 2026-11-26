using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Jobs;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Integração externa por consentimento (HiMarshall): o usuário emite tokens com escopo e o
/// consumidor troca `Bearer mbi_...` por dados mínimos — resumo de prontidão e vagas
/// internacionais filtradas pelo perfil de carreira.
/// </summary>
public static class IntegrationEndpoints
{
    /// <summary>Feature flag da integração HiMarshall (lado servidor).</summary>
    internal const string FeatureName = "integracao-himarshall";

    public static void MapIntegrationEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/integrations").WithTags("Integrações");

        // --- Gestão de tokens (sessão do usuário) ---

        group.MapPost("/tokens", async (
                [FromBody] CreateIntegrationTokenRequest? request,
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IFeatureFlags flags,
                CancellationToken cancellationToken) =>
            {
                if (!await flags.IsEnabledAsync(FeatureName, httpContext.User, cancellationToken))
                    return Results.NotFound();

                var userId = ResolveUserId(httpContext.User);
                if (userId is null)
                    return Results.Unauthorized();

                var name = request?.Name?.Trim() ?? string.Empty;
                if (name.Length is 0 or > 120)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["name"] = ["O nome do token deve ter 1–120 caracteres."]
                    });

                var scopes = request?.Scopes?
                    .Where(s => IIntegrationTokenService.SupportedScopes.Contains(s, StringComparer.OrdinalIgnoreCase))
                    .Select(s => s.ToLowerInvariant())
                    .Distinct()
                    .ToList() ?? [];

                if (scopes.Count == 0)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["scopes"] = [$"Informe ao menos um escopo válido: {string.Join(", ", IIntegrationTokenService.SupportedScopes)}."]
                    });

                var (token, rawValue) = await tokens.CreateAsync(
                    userId.Value, name, scopes, request?.ExpiresInDays ?? 90, cancellationToken: cancellationToken);

                httpContext.Response.Headers.CacheControl = "no-store";
                // O valor em claro só aparece nesta resposta — depois disso só o hash existe
                // (token nulo só ocorreria com requestId de dedup — a API não o envia)
                return Results.Ok(new
                {
                    token!.Id.Value,
                    token.Name,
                    token.Prefix,
                    Scopes = token.ScopeList,
                    token.ExpiresAt,
                    token.CreatedAt,
                    Token = rawValue
                });
            })
            .RequireAuthorization("ApiBearer");

        group.MapGet("/tokens", async (
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IFeatureFlags flags,
                CancellationToken cancellationToken) =>
            {
                if (!await flags.IsEnabledAsync(FeatureName, httpContext.User, cancellationToken))
                    return Results.NotFound();

                var userId = ResolveUserId(httpContext.User);
                if (userId is null)
                    return Results.Unauthorized();

                var list = await tokens.ListAsync(userId.Value, cancellationToken);
                return Results.Ok(list.Select(t => new
                {
                    t.Id.Value,
                    t.Name,
                    t.Prefix,
                    Scopes = t.ScopeList,
                    t.ExpiresAt,
                    t.RevokedAt,
                    t.LastUsedAt,
                    t.CreatedAt,
                    IsActive = t.IsActive
                }));
            })
            .RequireAuthorization("ApiBearer");

        group.MapDelete("/tokens/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IFeatureFlags flags,
                CancellationToken cancellationToken) =>
            {
                if (!await flags.IsEnabledAsync(FeatureName, httpContext.User, cancellationToken))
                    return Results.NotFound();

                var userId = ResolveUserId(httpContext.User);
                if (userId is null)
                    return Results.Unauthorized();

                return await tokens.RevokeAsync(userId.Value, id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .RequireAuthorization("ApiBearer");

        // --- Consumo pelo integrador (Bearer mbi_..., sem sessão) ---

        group.MapGet("/token-info", async (
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IIntegrationReadinessService readiness,
                IFeatureFlags flags,
                CancellationToken cancellationToken) =>
            {
                var (token, _, error) = await AuthorizeAsync(httpContext, tokens, readiness, flags, cancellationToken);
                return error ?? Results.Ok(new { scopes = token!.ScopeList });
            })
            .AllowAnonymous();

        group.MapGet("/readiness-summary", async (
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IIntegrationReadinessService readiness,
                IFeatureFlags flags,
                CancellationToken cancellationToken) =>
            {
                var (token, candidate, error) = await AuthorizeAsync(httpContext, tokens, readiness, flags, cancellationToken);
                if (error is not null) return error;
                if (!token!.HasScope("readiness:read"))
                    return Problem(StatusCodes.Status403Forbidden, "scope_missing");

                var summary = await readiness.BuildSummaryAsync(candidate!.Id.Value, cancellationToken);
                return summary is null ? Results.NotFound() : Results.Ok(new
                {
                    consentVersion = "2026-11",
                    provider = "monburan",
                    summary.CandidateId,
                    summary.Name,
                    summary.Email,
                    summary.ActivePlan,
                    summary.HasInternationalJobs,
                    summary.DesiredRole,
                    summary.Category,
                    summary.City,
                    summary.State,
                    summary.Mode,
                    summary.ContractType,
                    summary.MinimumSalary,
                    summary.PreferredCountries,
                    summary.ResumeCount,
                    summary.GeneratedAt
                });
            })
            .AllowAnonymous();

        group.MapGet("/jobs", async (
                HttpContext httpContext,
                IIntegrationTokenService tokens,
                IIntegrationReadinessService readiness,
                ListInternationalJobsUseCase useCase,
                IFeatureFlags flags,
                [FromQuery] int page = 1,
                [FromQuery] int size = 10,
                CancellationToken cancellationToken = default) =>
            {
                var (token, candidate, error) = await AuthorizeAsync(httpContext, tokens, readiness, flags, cancellationToken);
                if (error is not null) return error;
                if (!token!.HasScope("jobs:read"))
                    return Problem(StatusCodes.Status403Forbidden, "scope_missing");

                if (!await flags.IsEnabledAsync(JobEndpoints.InternationalJobsFeature, PrincipalOf(token.UserId), cancellationToken))
                    return Problem(StatusCodes.Status403Forbidden, "jobs_disabled");

                var result = await useCase.ExecuteAsync(
                    new ListInternationalJobsQuery(candidate!.Id.Value, page, size), cancellationToken);
                return result.Status == EResultStatus.DomainError
                    ? Problem(StatusCodes.Status403Forbidden, "jobs_plan_required", result.Message)
                    : result.ToHttpResult();
            })
            .AllowAnonymous();
    }

    private static async Task<(IntegrationToken? Token, Candidate? Candidate, IResult? Error)> AuthorizeAsync(
        HttpContext httpContext,
        IIntegrationTokenService tokens,
        IIntegrationReadinessService readiness,
        IFeatureFlags flags,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        var token = await tokens.ValidateAsync(ExtractBearer(httpContext), cancellationToken);
        if (token is null)
            return (null, null, Problem(StatusCodes.Status401Unauthorized, "token_invalid"));

        var candidate = await readiness.ResolveCandidateAsync(token.UserId, cancellationToken);
        if (candidate is null)
            return (null, null, Problem(StatusCodes.Status401Unauthorized, "token_invalid"));

        if (!await flags.IsEnabledAsync(FeatureName, PrincipalOf(token.UserId), cancellationToken))
            return (null, null, Problem(StatusCodes.Status403Forbidden, "integration_disabled"));

        return (token, candidate, null);
    }

    private static IResult Problem(int statusCode, string code, string? detail = null) =>
        Results.Problem(statusCode: statusCode, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    // A flag de grupo é avaliada para o dono do token, não para o chamador anônimo: a
    // identidade precisa vir autenticada e com user_type para render os grupos implícitos.
    private static ClaimsPrincipal PrincipalOf(Guid userId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimsExtensions.UserTypeClaimType, "candidate")
            ],
            authenticationType: "IntegrationToken"));

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string ExtractBearer(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : string.Empty;
    }

    public sealed record CreateIntegrationTokenRequest(string? Name, List<string>? Scopes, int ExpiresInDays = 90);
}
