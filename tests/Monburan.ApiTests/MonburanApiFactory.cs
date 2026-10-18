using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Monburan.Core.Interfaces.Services;
using Monburan.Web.Data;

namespace Monburan.ApiTests;

/// <summary>
/// WebApplicationFactory que troca o PostgreSQL por EF InMemory.
/// Roda em Development para manter o seed de dados (vagas, planos, usuários).
/// </summary>
public class MonburanApiFactory : WebApplicationFactory<Program>
{
    // Um banco por instância da factory: o seed de desenvolvimento roda a cada host
    // e não é idempotente, então cada classe de teste precisa de um store isolado.
    private readonly string _databaseName = $"monburan-api-tests-{Guid.NewGuid():N}";

    // Service provider EF interno exclusivo do InMemory — evita o conflito
    // "multiple database providers" com o Npgsql registrado pela aplicação.
    private static readonly IServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Os testes nunca falam com um provedor de IA de verdade, nem com segredo de usuário na máquina de quem roda
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = string.Empty,
                ["Ai:MaxRequestsPerHour"] = "100"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiTextClient>();
            services.AddSingleton<FakeAiTextClient>();
            services.AddSingleton<IAiTextClient>(provider => provider.GetRequiredService<FakeAiTextClient>());

            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll(typeof(IDbContextFactory<ApplicationDbContext>));

            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options
                    .UseInMemoryDatabase(_databaseName)
                    .UseInternalServiceProvider(EfServiceProvider)
                    // InMemory não suporta transações; mantemos o warning ignorado
                    // caso algum caminho use BeginTransactionAsync (o EF o transforma em erro).
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}
