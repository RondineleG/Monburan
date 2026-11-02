namespace Monburan.Web.Endpoints;

/// <summary>
/// Mapeia todos os endpoints REST consumidos pelo app mobile.
/// Auth fica em /api/auth (anônimo); o restante em /api/v1 com bearer JWT.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();

        var api = app.MapGroup("/api/v1").WithTags("API v1");

        api.MapJobEndpoints();
        api.MapJobAlertEndpoints();
        api.MapApplicationEndpoints();
        api.MapCandidateEndpoints();
        api.MapAccountEndpoints();
        api.MapAdvertiserEndpoints();
        api.MapResumeEndpoints();
        api.MapFileEndpoints();
        api.MapNotificationEndpoints();
        api.MapSubscriptionEndpoints();
        api.MapDashboardEndpoints();
        api.MapFeatureEndpoints();

        return app;
    }
}
