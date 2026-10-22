using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;

using Xunit;

namespace Monburan.IntegrationTests.Migrations;

/// <summary>
/// Migration da trilha do candidato: o backfill precisa escrever UMA linha por candidatura
/// existente, com o status atual e a data da candidatura — e não duplicar quem já tem histórico.
/// </summary>
public class ApplicationStatusHistoryBackfillTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public ApplicationStatusHistoryBackfillTests(PostgresDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Up_WhenApplicationsAlreadyExist_WritesTheCurrentStatusToTheHistory()
    {
        await using var context = _database.CreateContext();
        var migrator = context.GetService<IMigrator>();

        // Para uma versão antes da migration da trilha e grava candidaturas como eram antes dela
        await migrator.MigrateAsync("ApplicationResumeId");

        // A empresa e o candidato entram por SQL: o modelo atual já conhece colunas que o
        // schema antigo não tem (a coluna do plano ativo do candidato, por exemplo)
        var advertiserId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Advertisers" ("Id", "Name", "Email", "Status", "PersonType", "IsEmailVerified",
                                       "IsPhoneVerified", "CreatedAt", "CreatedBy", "DeletedBy", "IsDeleted",
                                       "TenantId", "UpdatedBy")
            VALUES ({advertiserId}, 'Empresa Teste', 'empresa.teste@teste.com', 'Incomplete', 0, false, false,
                    {DateTime.UtcNow}, 'teste', '', false, 'default', '')
            """);

        var candidateId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Candidates" ("Id", "Name", "Email", "Status", "IsEmailVerified", "IsPhoneVerified",
                                      "CreatedAt", "CreatedBy", "DeletedBy", "IsDeleted", "TenantId", "UpdatedBy")
            VALUES ({candidateId}, 'Candidato Teste', 'candidato.teste@teste.com', 'Incomplete', false, false,
                    {DateTime.UtcNow}, 'teste', '', false, 'default', '')
            """);

        // A vaga entra por SQL: o modelo atual já conhece colunas que o schema antigo não tem
        var jobId = Guid.NewGuid();
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Jobs" ("Id", "AdvertiserId", "Title", "Description", "ExpirationDate", "Status",
                                "CreatedAt", "CreatedBy", "DeletedBy", "IsDeleted", "TenantId", "UpdatedBy")
            VALUES ({jobId}, {advertiserId}, 'Desenvolvedor .NET', 'Descrição da vaga',
                    {DateTime.UtcNow.AddDays(30)}, 'Active', {DateTime.UtcNow}, 'teste', '', false, 'default', '')
            """);

        var application = new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(candidateId),
            JobId = Id.Create(jobId),
            Status = EApplicationStatus.Selected,
            TenantId = "default"
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        // Agora sim: aplica a migration com o backfill
        await migrator.MigrateAsync(null);

        await using var check = _database.CreateContext();
        var history = await check.ApplicationStatusHistory.AsNoTracking()
            .Where(item => item.ApplicationId == application.Id)
            .ToListAsync();

        history.Should().ContainSingle("cada candidatura existente recebe uma linha");
        history[0].Status.Should().Be(EApplicationStatus.Selected);
        history[0].Date.Should().BeCloseTo(application.CreatedAt, TimeSpan.FromMilliseconds(1), "a data da etapa é a data da candidatura");
    }

    [Fact]
    public async Task Up_IsIdempotentForApplicationsThatAlreadyHaveHistory()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();

        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Empresa Teste",
            Email = $"empresa.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Candidato Teste",
            Email = $"candidato.{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        };
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Desenvolvedor .NET",
            Description = "Descrição da vaga",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };
        var application = new Application
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = candidate.Id,
            JobId = job.Id,
            Status = EApplicationStatus.Sent,
            TenantId = "default"
        };
        context.Advertisers.Add(advertiser);
        context.Candidates.Add(candidate);
        context.Jobs.Add(job);
        context.Applications.Add(application);
        context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            Id = Id.Create(Guid.NewGuid()),
            ApplicationId = application.Id,
            Status = EApplicationStatus.Sent,
            Date = DateTime.UtcNow,
            TenantId = "default"
        });
        await context.SaveChangesAsync();

        // Mesmo SQL do backfill da migration: o WHERE NOT EXISTS é o que garante a idempotência
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ApplicationStatusHistory"
                ("Id", "ApplicationId", "Status", "Date", "Note", "Responsible", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "TenantId", "UpdatedAt", "UpdatedBy")
            SELECT gen_random_uuid(), a."Id", a."Status", a."CreatedAt", NULL, NULL, a."CreatedAt", a."CreatedBy", NULL, '', false, a."TenantId", NULL, ''
            FROM "Applications" a
            WHERE NOT EXISTS (
                SELECT 1 FROM "ApplicationStatusHistory" h WHERE h."ApplicationId" = a."Id")
            """);

        await using var check = _database.CreateContext();
        var count = await check.ApplicationStatusHistory.CountAsync(item => item.ApplicationId == application.Id);
        count.Should().Be(1, "quem já tem histórico não ganha linha duplicada");
    }
}
