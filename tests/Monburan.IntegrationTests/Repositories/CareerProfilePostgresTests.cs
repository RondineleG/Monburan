using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>Perfil de carreira no Postgres: um só por candidato, pelo índice único de CandidateId.</summary>
public class CareerProfilePostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public CareerProfilePostgresTests(PostgresDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task UniqueIndex_BlocksTwoProfilesForTheSameCandidate()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();

        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Candidato Teste",
            Email = $"candidato.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        context.Candidates.Add(candidate);

        context.CareerProfiles.Add(NewProfile(candidate.Id.Value, "Desenvolvedor"));
        await context.SaveChangesAsync();

        await using var other = _database.CreateContext();
        other.CareerProfiles.Add(NewProfile(candidate.Id.Value, "Analista"));

        var act = () => other.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("CandidateId é único na tabela de perfis");
    }

    private static CareerProfile NewProfile(Guid candidateId, string desiredRole) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(candidateId),
        DesiredRole = desiredRole,
        City = "São Paulo",
        TenantId = "default"
    };
}
