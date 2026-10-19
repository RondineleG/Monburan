using Microsoft.AspNetCore.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Monburan.ApiTests;

/// <summary>
/// Mesma aplicação da <see cref="MonburanProductionApiFactory"/>, porém com um check que falha de
/// propósito. Serve para provar que o <c>/health/ready</c> vira 503 quando um check quebra, sem
/// precisar derrubar o banco de verdade.
/// </summary>
public class MonburanFailingHealthCheckApiFactory : MonburanProductionApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
            services.AddHealthChecks().AddCheck(
                "teste-sempre-falho",
                () => HealthCheckResult.Unhealthy("falha proposital do teste")));
    }
}
