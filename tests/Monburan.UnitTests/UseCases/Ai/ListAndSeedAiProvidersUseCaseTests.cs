using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Ai;
using Monburan.Core.Validations;
using Monburan.UnitTests.Web;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Ai;

public class ListAiProvidersUseCaseTests
{
    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiSecretReader _secrets = Substitute.For<IAiSecretReader>();

    [Fact]
    public async Task ListsTheDefaultFirst_FlaggingWhichKeysExistOnTheServer()
    {
        _repository.Add(AiTestData.Entity("Zeta", envVar: "AI_KEY_ZETA"));
        _repository.Add(AiTestData.Entity("Beta", isDefault: true, envVar: "AI_KEY_BETA"));
        _repository.Add(AiTestData.Entity("Alfa", envVar: "AI_KEY_ALFA"));
        _secrets.IsSet("AI_KEY_BETA").Returns(true);

        var result = await new ListAiProvidersUseCase(_repository, _secrets).ExecuteAsync();

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Select(provider => provider.Name).Should().Equal("Beta", "Alfa", "Zeta");
        result.Data!.Single(provider => provider.Name == "Beta").KeyConfigured.Should().BeTrue();
        result.Data!.Where(provider => provider.Name != "Beta").Should().OnlyContain(provider => !provider.KeyConfigured);
        _secrets.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact]
    public async Task PropagatesARepositoryFailure()
    {
        var repository = Substitute.For<IAiProviderRepository>();
        repository.ListAsync().Returns(RequestResult<IEnumerable<AiProvider>>.WithError("banco fora do ar"));

        var result = await new ListAiProvidersUseCase(repository, _secrets).ExecuteAsync();

        result.Status.Should().Be(EResultStatus.HasError);
    }

    [Fact]
    public void NoTypeThatReachesTheDatabaseOrTheScreenHasAPlaceToHoldTheKey()
    {
        // O banco e a tela só conhecem o NOME da variável de ambiente; a chave nunca tem onde ficar
        var forbidden = new[] { "ApiKey", "Key", "Secret", "Token", "Password", "Credential" };
        var types = new[] { typeof(AiProvider), typeof(AiProviderSettings), typeof(AiProviderResponse), typeof(Monburan.Core.DTOs.Request.AiProviderRequest) };

        foreach (var type in types)
        {
            type.GetProperties().Select(property => property.Name)
                .Should().NotContain(forbidden, because: $"{type.Name} não pode carregar a chave");
        }
    }
}

public class SeedAiProvidersUseCaseTests
{
    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiProviderCatalog _catalog = Substitute.For<IAiProviderCatalog>();

    [Fact]
    public async Task OnTheFirstRun_RegistersThePresetsMarkedForIt_WithXiaomiAsTheEnabledDefault()
    {
        var created = await new SeedAiProvidersUseCase(_repository, _catalog).ExecuteAsync();

        var seeded = AiProviderPresets.All.Where(preset => preset.SeedOnFirstRun).ToList();
        created.Should().Be(seeded.Count);
        _repository.Items.Select(provider => provider.Name).Should().BeEquivalentTo(seeded.Select(preset => preset.Label));

        var standard = _repository.Items.Single(provider => provider.IsDefault);
        standard.Name.Should().Be("Xiaomi MiMo");
        standard.IsEnabled.Should().BeTrue();
        standard.Model.Should().Be("mimo-v2.6-pro");
        standard.ApiKeyEnvVar.Should().Be("AI_KEY_XIAOMI");

        // Os demais nascem desativados: só ligam quando o administrador cadastrar a chave e ativar
        _repository.Items.Where(provider => !provider.IsDefault).Should().OnlyContain(provider => !provider.IsEnabled);
        await _catalog.Received(1).ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EverySeededProvider_PassesTheSameRulesTheAdministratorScreenEnforces()
    {
        await new SeedAiProvidersUseCase(_repository, _catalog).ExecuteAsync();

        foreach (var provider in _repository.Items)
        {
            AiProviderRules.IsValidEnvVarName(provider.ApiKeyEnvVar).Should().BeTrue(provider.Name);
            AiProviderRules.TryParseBaseUrl(provider.BaseUrl, out _, out _).Should().BeTrue(provider.Name);
            provider.Model.Should().NotBeNullOrWhiteSpace(provider.Name);
            provider.CreatedBy.Should().Be("sistema");
        }
    }

    [Fact]
    public async Task DoesNothing_WhenThereIsAlreadyAProvider_SoTheAdministratorKeepsControl()
    {
        _repository.Add(AiTestData.Entity("Meu provedor", isDefault: true));

        var created = await new SeedAiProvidersUseCase(_repository, _catalog).ExecuteAsync();

        created.Should().Be(0);
        _repository.Items.Should().ContainSingle();
        _repository.CreateCalls.Should().Be(0);
        await _catalog.DidNotReceive().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotBringBackAProviderTheAdministratorDeleted_AsLongAsAnotherExists()
    {
        await new SeedAiProvidersUseCase(_repository, _catalog).ExecuteAsync();
        var deepseek = _repository.Items.Single(provider => provider.Name == "DeepSeek");
        await _repository.DeleteAsync(deepseek.Id);

        var created = await new SeedAiProvidersUseCase(_repository, _catalog).ExecuteAsync();

        created.Should().Be(0);
        _repository.Items.Should().NotContain(provider => provider.Name == "DeepSeek");
    }
}
