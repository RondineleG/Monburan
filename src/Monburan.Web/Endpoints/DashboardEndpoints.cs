using Monburan.Web.Services;
using Monburan.Web.UseCases.Analytics;

namespace Monburan.Web.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async (
            ICurrentUserHelper currentUser,
            GetDashboardAnalyticsUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var advertiserId = currentUser.GetAdvertiserId();
            if (advertiserId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil de anunciante necessário");

            var result = await useCase.ExecuteAsync(advertiserId.Value, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization("ApiBearer")
        .WithTags("Dashboard")
        .WithSummary("Analytics do anunciante autenticado");

        return api;
    }
}
