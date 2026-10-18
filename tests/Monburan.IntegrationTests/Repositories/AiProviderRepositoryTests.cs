using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

public class AiProviderRepositoryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AiProviderRepository _repository;

    public AiProviderRepositoryTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));

        _serviceProvider = services.BuildServiceProvider();
        _repository = new AiProviderRepository(_serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
    }

    public void Dispose() => _serviceProvider.Dispose();

    private static AiProvider Provider(string name, bool enabled = true, bool isDefault = false) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        Name = name,
        Protocol = EAiProtocol.OpenAiCompatible,
        BaseUrl = "https://api.exemplo.com/v1",
        Model = "modelo-de-teste",
        ApiKeyEnvVar = "AI_KEY_TESTE",
        IsEnabled = enabled,
        IsDefault = isDefault,
        CreatedBy = "teste",
        UpdatedBy = "teste"
    };

    private async Task<AiProvider> AddAsync(string name, bool enabled = true, bool isDefault = false)
    {
        var created = await _repository.CreateAsync(Provider(name, enabled, isDefault));
        return created.Data!;
    }

    private async Task<AiProvider> ReloadAsync(AiProvider provider) => (await _repository.GetByIdAsync(provider.Id)).Data!;

    [Fact]
    public async Task CreateAsync_ThenGetByIdAsync_KeepsEveryField()
    {
        var provider = Provider("Claude");
        provider.Protocol = EAiProtocol.Anthropic;
        provider.BaseUrl = "https://api.anthropic.com";
        provider.Model = "claude-haiku-5-5";
        provider.ApiKeyEnvVar = "AI_KEY_ANTHROPIC";
        provider.TimeoutSeconds = 45;
        provider.MaxOutputTokens = 2048;
        provider.Temperature = 0.9;
        provider.SendTemperature = false;
        provider.UseJsonResponseFormat = false;
        provider.UseMaxCompletionTokens = true;

        await _repository.CreateAsync(provider);
        var loaded = (await _repository.GetByIdAsync(provider.Id)).Data!;

        loaded.Should().BeEquivalentTo(provider, options => options.Excluding(item => item.UpdatedAt));
    }

    [Fact]
    public async Task ListAsync_PutsTheDefaultFirst_ThenOrdersByName_AndHidesDeleted()
    {
        await AddAsync("Zeta");
        await AddAsync("Alfa");
        var standard = await AddAsync("Meio", isDefault: true);
        var removed = await AddAsync("Removido");
        await _repository.DeleteAsync(removed.Id);

        var list = (await _repository.ListAsync()).Data!.ToList();

        list.Select(provider => provider.Name).Should().Equal("Meio", "Alfa", "Zeta");
        list[0].Id.Should().Be(standard.Id);
    }

    [Fact]
    public async Task SetDefaultAsync_MovesTheFlag_LeavingExactlyOneDefault()
    {
        var first = await AddAsync("Primeiro", isDefault: true);
        var second = await AddAsync("Segundo");
        var third = await AddAsync("Terceiro");

        var result = await _repository.SetDefaultAsync(third.Id, "admin@monburan.com.br");

        result.Data!.IsDefault.Should().BeTrue();
        (await ReloadAsync(first)).IsDefault.Should().BeFalse();
        (await ReloadAsync(second)).IsDefault.Should().BeFalse();
        (await ReloadAsync(third)).IsDefault.Should().BeTrue();
        (await _repository.ListAsync()).Data!.Count(provider => provider.IsDefault).Should().Be(1);
    }

    [Fact]
    public async Task SetDefaultAsync_RecordsWhoChangedIt_OnBothProviders()
    {
        var first = await AddAsync("Primeiro", isDefault: true);
        var second = await AddAsync("Segundo");

        await _repository.SetDefaultAsync(second.Id, "admin@monburan.com.br");

        (await ReloadAsync(first)).UpdatedBy.Should().Be("admin@monburan.com.br");
        var reloaded = await ReloadAsync(second);
        reloaded.UpdatedBy.Should().Be("admin@monburan.com.br");
        reloaded.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetDefaultAsync_IsIdempotent()
    {
        var only = await AddAsync("Único", isDefault: true);

        var result = await _repository.SetDefaultAsync(only.Id, "admin");

        result.Status.Should().Be(EResultStatus.Success);
        (await ReloadAsync(only)).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_ForAnUnknownProvider_ReturnsNotFound_AndKeepsTheCurrentDefault()
    {
        var current = await AddAsync("Atual", isDefault: true);

        var result = await _repository.SetDefaultAsync(Id.Create(Guid.NewGuid()), "admin");

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        (await ReloadAsync(current)).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefaultAsync_IgnoresDeletedProviders()
    {
        var removed = await AddAsync("Removido");
        await _repository.DeleteAsync(removed.Id);

        var result = await _repository.SetDefaultAsync(removed.Id, "admin");

        result.Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task DeleteAsync_IsLogical_AndClearsTheDefaultAndEnabledFlags()
    {
        var standard = await AddAsync("Padrão", isDefault: true);

        var result = await _repository.DeleteAsync(standard.Id);

        result.Status.Should().Be(EResultStatus.Success);
        (await _repository.GetByIdAsync(standard.Id)).Status.Should().Be(EResultStatus.EntityNotFound);
        (await _repository.CountAsync()).Should().Be(0);

        using var scope = _serviceProvider.CreateScope();
        var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var row = await context.AiProviders.SingleAsync(provider => provider.Id == standard.Id);
        row.IsDeleted.Should().BeTrue();
        row.DeletedAt.Should().NotBeNull();
        row.IsDefault.Should().BeFalse();
        row.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OfAnUnknownProvider_ReturnsNotFound()
    {
        (await _repository.DeleteAsync(Id.Create(Guid.NewGuid()))).Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task NameExistsAsync_IgnoresCase_ExcludesTheProviderItself_AndIgnoresDeleted()
    {
        var provider = await AddAsync("OpenRouter");
        var removed = await AddAsync("Antigo");
        await _repository.DeleteAsync(removed.Id);

        (await _repository.NameExistsAsync("openrouter")).Should().BeTrue();
        (await _repository.NameExistsAsync("  OPENROUTER ")).Should().BeTrue();
        (await _repository.NameExistsAsync("OpenRouter", provider.Id)).Should().BeFalse();
        (await _repository.NameExistsAsync("Outro nome")).Should().BeFalse();
        (await _repository.NameExistsAsync("Antigo")).Should().BeFalse("o nome de um provedor excluído fica livre");
    }

    [Fact]
    public async Task CountAsync_CountsOnlyProvidersThatWereNotDeleted()
    {
        await AddAsync("Um");
        var two = await AddAsync("Dois");
        await AddAsync("Três");
        await _repository.DeleteAsync(two.Id);

        (await _repository.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheEditableFields_ButNeverTheDefaultFlag()
    {
        var standard = await AddAsync("Padrão", isDefault: true);
        var edited = Provider("Padrão renomeado", enabled: true, isDefault: false);
        edited.Id = standard.Id;
        edited.Model = "outro-modelo";
        edited.BaseUrl = "https://outro.exemplo.com/v1";
        edited.ApiKeyEnvVar = "AI_KEY_OUTRA";
        edited.Protocol = EAiProtocol.Anthropic;
        edited.UpdatedBy = "admin@monburan.com.br";

        var result = await _repository.UpdateAsync(edited);

        result.Status.Should().Be(EResultStatus.Success);
        var reloaded = await ReloadAsync(standard);
        reloaded.Name.Should().Be("Padrão renomeado");
        reloaded.Model.Should().Be("outro-modelo");
        reloaded.BaseUrl.Should().Be("https://outro.exemplo.com/v1");
        reloaded.ApiKeyEnvVar.Should().Be("AI_KEY_OUTRA");
        reloaded.Protocol.Should().Be(EAiProtocol.Anthropic);
        reloaded.UpdatedBy.Should().Be("admin@monburan.com.br");
        reloaded.UpdatedAt.Should().NotBeNull();
        reloaded.IsDefault.Should().BeTrue("o padrão só muda por SetDefaultAsync");
        reloaded.CreatedBy.Should().Be("teste");
    }

    [Fact]
    public async Task UpdateAsync_OfAnUnknownProvider_ReturnsNotFound()
    {
        (await _repository.UpdateAsync(Provider("Fantasma"))).Status.Should().Be(EResultStatus.EntityNotFound);
    }
}
