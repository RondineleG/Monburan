using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.IntegrationTests.Infrastructure;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

/// <summary>
/// A busca textual no Postgres usa ILike, que aproveita o índice de trigramas (pg_trgm).
/// Com ToLower().Contains o banco fazia varredura sequencial: o índice comum não serve
/// para um padrão com "%" na frente. Estes testes rodam no banco de verdade, onde a
/// extensão e os índices existem de fato.
/// </summary>
public class TextSearchPostgresTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public TextSearchPostgresTests(PostgresDatabase database)
    {
        _database = database;
    }

    // Marcador único por teste: a base é compartilhada entre os testes da classe, e um termo
    // genérico ("engenheiro") encontraria as vagas semeadas pelos outros testes
    private static string Marker() => "zz" + Guid.NewGuid().ToString("N")[..10];

    private async Task<JobRepository> SeedAsync(ApplicationDbContext context, string title, string category)
    {
        var advertiserId = Id.Create(Guid.NewGuid());
        context.Advertisers.Add(new Advertiser
        {
            Id = advertiserId,
            Name = "Empresa " + Guid.NewGuid().ToString("N")[..6],
            Email = $"empresa-{Guid.NewGuid():N}@teste.com",
            TenantId = "default"
        });

        context.Jobs.Add(new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiserId,
            Title = title,
            Description = "Descrição " + Guid.NewGuid().ToString("N")[..6],
            Category = category,
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        });

        await context.SaveChangesAsync();
        return new JobRepository(_database.ContextFactory);
    }

    [Fact]
    public async Task SearchAsync_FindsByPartOfTheTitle_IgnoringCase()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var marker = Marker();
        var jobs = await SeedAsync(context, $"Engenheiro {marker} de Software", "Tecnologia");

        var result = await jobs.SearchAsync(searchTerm: marker.ToUpperInvariant(), size: 50);

        result.Status.Should().Be(Core.Enums.EResultStatus.Success);
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Data.Should().ContainSingle(job => job.Title.Contains(marker));
    }

    [Fact]
    public async Task SearchAsync_FindsByMiddleOfTheWord_WhichNeedsTheTrigramIndex()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        // A palavra procurada está no meio do título: um índice comum (btree) não serve,
        // e é o caso de uso do índice de trigramas
        var marker = Marker();
        var jobs = await SeedAsync(context, $"Desenv{marker}olvedor .NET Sênior", "Tecnologia");

        var result = await jobs.SearchAsync(searchTerm: marker, size: 50);

        result.Data!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_FindsByTheDescription()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
        var marker = Marker();
        var jobs = await SeedAsync(context, "Vaga qualquer", "Tecnologia");

        // A descrição tem o marcador; a busca cobre o título, a descrição e a categoria
        var withMarker = await SeedAsync(context, "Outra vaga", "Tecnologia");
        await using (var seedContext = _database.CreateContext())
        {
            var job = await seedContext.Jobs.AsNoTracking()
                .FirstAsync(item => item.Title == "Outra vaga");
            job.Description = $"Descrição {marker} do currículo";
            seedContext.Jobs.Update(job);
            await seedContext.SaveChangesAsync();
        }

        var result = await withMarker.SearchAsync(searchTerm: marker, size: 50);

        result.Data!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task TheTrigramAndFunctionalIndexesExist()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();

        var indexes = await context.Database
            .SqlQueryRaw<string>("SELECT indexname FROM pg_indexes WHERE tablename IN ('Jobs', 'Candidates', 'Advertisers')")
            .ToListAsync();

        indexes.Should().Contain("IX_Jobs_Title_Trgm");
        indexes.Should().Contain("IX_Candidates_Email_Lower");
        indexes.Should().Contain("IX_Advertisers_Email_Lower");
    }

    [Fact]
    public async Task TheSearchQueryCanBeServedByTheTrigramIndex()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();

        // Com a tabela pequena o planeificador escolhe a varredura (custa menos). Desligando
        // a varredura sequencial, o plano mostra o que o banco usaria se a tabela crescesse:
        // se o índice de trigramas não servisse a consulta, o plano seria seq scan de novo.
        // A conexão fica aberta para o SET valer no EXPLAIN (o pool devolve a conexão entre
        // os comandos).
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET enable_seqscan = off");

        var plan = await context.Database
            .SqlQueryRaw<string>(
                "EXPLAIN SELECT * FROM \"Jobs\" WHERE lower(\"Title\") LIKE '%engenheiro%'")
            .ToListAsync();

        await context.Database.CloseConnectionAsync();

        string.Join(" ", plan).Should().Contain("IX_Jobs_Title_Trgm",
            "a busca por substring precisa poder bater no índice de trigramas, não na varredura da tabela");
    }

    [Fact]
    public async Task TheEmailLookupCanBeServedByAnIndex()
    {
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();

        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET enable_seqscan = off");

        // A consulta por e-mail (a mais usada do produto: login, troca de e-mail, listagem)
        // compara lower("Email"); os índices sobre essa expressão são o que a faz deixar de
        // varrer a tabela
        var plan = await context.Database
            .SqlQueryRaw<string>(
                "EXPLAIN SELECT * FROM \"Candidates\" WHERE lower(\"Email\") = 'alguem@teste.com'")
            .ToListAsync();

        await context.Database.CloseConnectionAsync();

        var explain = string.Join(" ", plan);
        explain.Should().MatchRegex("IX_Candidates_Email_(Lower|Trgm)",
            "a consulta por e-mail precisa bater em um índice, não na varredura da tabela");
    }
}
