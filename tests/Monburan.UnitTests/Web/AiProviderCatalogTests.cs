using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Web.Services;

using NSubstitute;

namespace Monburan.UnitTests.Web;

public class AiProviderCatalogTests
{
    private readonly IAiProviderRepository _repository = Substitute.For<IAiProviderRepository>();

    private AiProviderCatalog Catalog()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _repository);
        var provider = services.BuildServiceProvider();
        return new AiProviderCatalog(provider.GetRequiredService<IServiceScopeFactory>(), new AiListLogger<AiProviderCatalog>());
    }

    private void Stored(params AiProvider[] providers) =>
        _repository.ListAsync().Returns(RequestResult<IEnumerable<AiProvider>>.Success(providers));

    [Fact]
    public async Task IsEmptyUntilTheFirstReload()
    {
        var catalog = Catalog();

        catalog.Default.Should().BeNull();
        catalog.All.Should().BeEmpty();

        Stored(AiTestData.Entity("A", isDefault: true));
        await catalog.ReloadAsync();

        catalog.All.Should().ContainSingle();
    }

    [Fact]
    public async Task ReloadAsync_ExposesEveryProviderAndTheEnabledDefault()
    {
        var standard = AiTestData.Entity("Padrão", isDefault: true);
        Stored(standard, AiTestData.Entity("Outro"), AiTestData.Entity("Desativado", enabled: false));
        var catalog = Catalog();

        await catalog.ReloadAsync();

        catalog.All.Should().HaveCount(3);
        catalog.Default.Should().NotBeNull();
        catalog.Default!.Id.Should().Be(standard.Id.Value);
        catalog.Default.Name.Should().Be("Padrão");
        catalog.Default.ApiKeyEnvVar.Should().Be("AI_KEY_TESTE");
    }

    [Fact]
    public async Task ReloadAsync_HasNoDefault_WhenTheDefaultIsDisabled_SoTheAiTurnsOff()
    {
        Stored(AiTestData.Entity("Padrão", enabled: false, isDefault: true), AiTestData.Entity("Outro"));
        var catalog = Catalog();

        await catalog.ReloadAsync();

        catalog.Default.Should().BeNull();
        catalog.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReloadAsync_KeepsThePreviousListWhenTheRepositoryFails()
    {
        Stored(AiTestData.Entity("Padrão", isDefault: true));
        var catalog = Catalog();
        await catalog.ReloadAsync();

        _repository.ListAsync().Returns(RequestResult<IEnumerable<AiProvider>>.WithError("banco fora do ar"));
        await catalog.ReloadAsync();

        catalog.Default.Should().NotBeNull();
        catalog.All.Should().ContainSingle();
    }

    [Fact]
    public async Task ReloadAsync_KeepsThePreviousListWhenTheRepositoryThrows()
    {
        Stored(AiTestData.Entity("Padrão", isDefault: true));
        var catalog = Catalog();
        await catalog.ReloadAsync();

        _repository.ListAsync().Returns<Task<RequestResult<IEnumerable<AiProvider>>>>(_ => throw new InvalidOperationException("falha"));
        var act = () => catalog.ReloadAsync();

        await act.Should().NotThrowAsync();
        catalog.Default.Should().NotBeNull();
    }

    [Fact]
    public async Task ReloadAsync_PicksUpChangesMadeByTheAdministrator()
    {
        var first = AiTestData.Entity("Primeiro", isDefault: true);
        var second = AiTestData.Entity("Segundo");
        Stored(first, second);
        var catalog = Catalog();
        await catalog.ReloadAsync();
        catalog.Default!.Name.Should().Be("Primeiro");

        first.IsDefault = false;
        second.IsDefault = true;
        Stored(first, second);
        await catalog.ReloadAsync();

        catalog.Default!.Name.Should().Be("Segundo");
    }
}
