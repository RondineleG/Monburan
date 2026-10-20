using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Web.Data;

using Testcontainers.PostgreSql;

namespace Monburan.IntegrationTests.Infrastructure;

/// <summary>
/// Postgres de verdade em container, uma instância por classe de teste: índice único, cascade de FK
/// e ExecuteUpdateAsync só existem no banco relacional. Cada classe decide quando migrar (quem testa
/// a própria migration precisa controlar a versão aplicada).
/// </summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(_container.GetConnectionString()));
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_container.GetConnectionString()));
        _services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync().AsTask();
    }

    public ApplicationDbContext CreateContext() =>
        _services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

    public IDbContextFactory<ApplicationDbContext> ContextFactory =>
        _services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
}
