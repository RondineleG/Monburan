using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Monburan.Web.Data;

namespace Monburan.UnitTests.Web;

/// <summary>Factory de contexto EF InMemory para testes de serviços que recebem IDbContextFactory.</summary>
internal sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory(string? databaseName = null)
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"monburan-unit-{Guid.NewGuid():N}")
            // InMemory não suporta transações: serviços que abrem BeginTransaction falhariam sem ignorar o warning.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    public ApplicationDbContext CreateDbContext() => new(_options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
