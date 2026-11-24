using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Monburan.Core.Entities;
using Monburan.IntegrationTests.Infrastructure;

using Xunit;

namespace Monburan.IntegrationTests.Migrations;

/// <summary>
/// O dedup de reemissão depende do índice único em IntegrationTokens.RequestId: sem ele,
/// dois envios concorrentes do formulário criam dois tokens para o mesmo RequestId.
/// </summary>
public class IntegrationTokenRequestIdIndexTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public IntegrationTokenRequestIdIndexTests(PostgresDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task RequestId_QuandoRepetido_RejeitaOSegundoInsert()
    {
        await using var context = _database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync();

        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.IntegrationTokens.Add(new IntegrationToken
        {
            UserId = userId,
            RequestId = requestId,
            Name = "HiMarshall",
            Prefix = "mbi_aaaa",
            TokenHash = new string('a', 64),
            Scopes = "readiness:read",
        });
        await context.SaveChangesAsync();

        context.IntegrationTokens.Add(new IntegrationToken
        {
            UserId = userId,
            RequestId = requestId,
            Name = "HiMarshall",
            Prefix = "mbi_bbbb",
            TokenHash = new string('b', 64),
            Scopes = "readiness:read",
        });
        var salvar = async () => await context.SaveChangesAsync();

        var falha = await salvar.Should().ThrowAsync<DbUpdateException>();
        var postgres = falha.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        postgres.SqlState.Should().Be(Npgsql.PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("IX_IntegrationTokens_RequestId");
    }
}
