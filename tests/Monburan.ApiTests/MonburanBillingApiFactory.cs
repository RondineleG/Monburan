using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>
/// Factory com credenciais fictícias do Mercado Pago e o HttpClient tipado do
/// <see cref="MercadoPagoBillingService"/> apontado para um handler em memória —
/// nenhum teste fala com api.mercadopago.com de verdade.
/// </summary>
public class MonburanBillingApiFactory : MonburanApiFactory
{
    public const string WebhookSecret = "segredo-de-teste-do-webhook";

    public StubHttpMessageHandler MercadoPago { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:MercadoPago:AccessToken"] = "APP-TEST-TOKEN",
                ["Billing:MercadoPago:WebhookSecret"] = WebhookSecret,
                ["Billing:MercadoPago:PublicBaseUrl"] = "https://monburan.test",
                ["App:PublicBaseUrl"] = "https://monburan.test"
            }));

        builder.ConfigureServices(services =>
            services.AddHttpClient<MercadoPagoBillingService>()
                .ConfigurePrimaryHttpMessageHandler(_ => MercadoPago));
    }
}
