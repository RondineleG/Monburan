using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Monburan.Web.Data;
using Monburan.Web.Models;

namespace Monburan.ApiTests;

/// <summary>Massa de demonstração do ambiente de homologação: volume, contas e textos sem cara de teste.</summary>
public sealed class DemoSeedDataTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Homologa#2026";

    private readonly MonburanApiFactory _factory;

    public DemoSeedDataTests(MonburanApiFactory factory) => _factory = factory;

    private async Task SeedAsync(string? password = Password)
    {
        using var scope = _factory.Services.CreateScope();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = password })
            .Build();
        await DemoSeedData.InitializeAsync(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            configuration,
            NullLogger.Instance);
    }

    [Fact]
    public async Task Seed_CreatesARealisticVolume_WithLoginsAndAnAdmin_AndRunsOnlyOnce()
    {
        await SeedAsync();
        await SeedAsync(); // segunda vez não duplica

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = "@" + DemoSeedData.Domain;

        (await db.Advertisers.CountAsync(a => a.Email.EndsWith(suffix))).Should().Be(12);
        (await db.Candidates.CountAsync(c => c.Email.EndsWith(suffix))).Should().Be(40);
        (await db.Jobs.CountAsync(j => j.Title == "Motorista Carreteiro")).Should().BeGreaterThanOrEqualTo(2);
        (await db.Jobs.CountAsync(j => j.AdvertiserId == null)).Should().BeGreaterThanOrEqualTo(3);

        var candidateIds = await db.Candidates.Where(c => c.Email.EndsWith(suffix)).Select(c => c.Id).ToListAsync();
        (await db.Applications.CountAsync(a => candidateIds.Contains(a.CandidateId))).Should().Be(120);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync("admin" + suffix);
        admin.Should().NotBeNull();
        (await users.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
        var someone = await users.FindByEmailAsync((await db.Candidates.FirstAsync(c => c.Email.EndsWith(suffix))).Email);
        (await users.CheckPasswordAsync(someone!, Password)).Should().BeTrue();
    }

    [Fact]
    public async Task Seed_HasNoTestLookingNames()
    {
        await SeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = "@" + DemoSeedData.Domain;
        var texts = new List<string>();
        texts.AddRange(await db.Candidates.Where(c => c.Email.EndsWith(suffix)).Select(c => c.Name + " " + c.Email).ToListAsync());
        texts.AddRange(await db.Advertisers.Where(a => a.Email.EndsWith(suffix)).Select(a => a.Name + " " + a.Email + " " + a.CompanyDescription).ToListAsync());
        texts.AddRange(await db.Jobs.Select(j => j.Title + " " + j.Description).ToListAsync());

        texts.Should().NotContain(t => t.Contains("teste", StringComparison.OrdinalIgnoreCase)
            || t.Contains("test", StringComparison.OrdinalIgnoreCase)
            || t.Contains("e2e", StringComparison.OrdinalIgnoreCase)
            || t.Contains("lorem", StringComparison.OrdinalIgnoreCase));
    }
}
