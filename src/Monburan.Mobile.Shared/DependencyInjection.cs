using Microsoft.Extensions.DependencyInjection;

using Monburan.Mobile.Shared.Api;
using Monburan.Mobile.Shared.Auth;
using Monburan.Mobile.Shared.Ui;

namespace Monburan.Mobile.Shared;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os clientes HTTP (com e sem bearer), TokenService, AuthStateService
    /// e os clientes tipados da API.
    /// </summary>
    public static IServiceCollection AddMonburanApi(this IServiceCollection services, Uri baseUrl)
    {
        services.AddTransient<AuthHeaderHandler>();

        // Timeout curto: o default de 100s deixa a UI pendurada em cold-start offline
        services.AddHttpClient(ApiClientNames.Api, client =>
            {
                client.BaseAddress = baseUrl;
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>();

        // Um PDF de 10 MB em 4G fraco passa dos 25 s: o envio de arquivo tem cliente próprio, com mais tempo
        services.AddHttpClient(ApiClientNames.Upload, client =>
            {
                client.BaseAddress = baseUrl;
                client.Timeout = TimeSpan.FromMinutes(3);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddHttpClient(ApiClientNames.Auth, client =>
        {
            client.BaseAddress = baseUrl;
            client.Timeout = TimeSpan.FromSeconds(25);
        });

        services.AddSingleton<TokenService>();
        services.AddSingleton<AuthStateService>();
        services.AddSingleton<MonburanApiClient>();
        services.AddSingleton<AuthApiClient>();
        services.AddSingleton<IToastService, ToastService>();

        return services;
    }
}
