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
/// <see cref="WebApplicationFactory{TEntryPoint}"/> rodando em <c>Production</c> com o PostgreSQL
/// trocado pelo EF InMemory. Existe para provar que o contrato de erro da API é o mesmo nos dois
/// ambientes: em Development a página de exceção do desenvolvedor não pode vazar stack trace.
/// </summary>
public class MonburanProductionApiFactory : WebApplicationFactory<Program>
{
    // Um banco por instância da factory: o seed roda por host e não é idempotente.
    private readonly string _databaseName = $"monburan-api-tests-prod-{Guid.NewGuid():N}";

    // Service provider EF interno exclusivo do InMemory — evita o conflito
    // "multiple database providers" com o Npgsql registrado pela aplicação.
    private static readonly IServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A chave do JWT é obrigatória em produção (validação na inicialização do host).
                ["Jwt:SigningKey"] = "chave-de-teste-producao-nao-usada-em-linha-7f3a9c1e5b8d4f6a",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=monburan_test;Username=postgres;Password=postgres",
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
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}
