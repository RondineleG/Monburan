using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Ai;
using Monburan.UnitTests.Web;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Ai;

public class SetDefaultAiProviderUseCaseTests
{
    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiProviderCatalog _catalog = Substitute.For<IAiProviderCatalog>();
    private readonly IAiSecretReader _secrets = Substitute.For<IAiSecretReader>();

    private SetDefaultAiProviderUseCase UseCase() => new(_repository, _catalog, _secrets);

    private static AiProviderActionRequest Action(Guid id) => new() { Id = id, UpdatedBy = "admin@monburan.com.br" };

    [Fact]
    public async Task MovesTheDefault_AndReloadsTheCatalog()
    {
        var current = _repository.Add(AiTestData.Entity("Atual", isDefault: true));
        var next = _repository.Add(AiTestData.Entity("Novo"));

        var result = await UseCase().ExecuteAsync(Action(next.Id.Value));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsDefault.Should().BeTrue();
        next.IsDefault.Should().BeTrue();
        current.IsDefault.Should().BeFalse();
        _repository.LastSetDefaultBy.Should().Be("admin@monburan.com.br");
        await _catalog.Received(1).ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefusesADisabledProvider_WithAMessageSayingToEnableItFirst()
    {
        var disabled = _repository.Add(AiTestData.Entity("Desativado", enabled: false));

        var result = await UseCase().ExecuteAsync(Action(disabled.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Ative");
        disabled.IsDefault.Should().BeFalse();
        _repository.SetDefaultCalls.Should().Be(0);
    }

    [Fact]
    public async Task ReturnsNotFound_ForAnUnknownProvider()
    {
        var result = await UseCase().ExecuteAsync(Action(Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _catalog.DidNotReceive().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportsWhetherTheKeyExists()
    {
        var provider = _repository.Add(AiTestData.Entity("Com chave", envVar: "AI_KEY_COM"));
        _secrets.IsSet("AI_KEY_COM").Returns(true);

        var result = await UseCase().ExecuteAsync(Action(provider.Id.Value));

        result.Data!.KeyConfigured.Should().BeTrue();
    }
}

public class DeleteAiProviderUseCaseTests
{
    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiProviderCatalog _catalog = Substitute.For<IAiProviderCatalog>();

    private DeleteAiProviderUseCase UseCase() => new(_repository, _catalog);

    private static AiProviderActionRequest Action(Guid id) => new() { Id = id, UpdatedBy = "admin@monburan.com.br" };

    [Fact]
    public async Task DeletesANonDefaultProvider_AndReloadsTheCatalog()
    {
        _repository.Add(AiTestData.Entity("Padrão", isDefault: true));
        var other = _repository.Add(AiTestData.Entity("Outro"));

        var result = await UseCase().ExecuteAsync(Action(other.Id.Value));

        result.Status.Should().Be(EResultStatus.Success);
        _repository.Items.Should().ContainSingle().Which.Name.Should().Be("Padrão");
        await _catalog.Received(1).ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefusesToDeleteTheDefault_WhileOtherProvidersExist()
    {
        var standard = _repository.Add(AiTestData.Entity("Padrão", isDefault: true));
        _repository.Add(AiTestData.Entity("Outro"));

        var result = await UseCase().ExecuteAsync(Action(standard.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("outro provedor como padrão");
        _repository.Items.Should().HaveCount(2);
        await _catalog.DidNotReceive().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletesTheDefault_WhenItIsTheOnlyProvider_AndTheAiTurnsOff()
    {
        var only = _repository.Add(AiTestData.Entity("Único", isDefault: true));

        var result = await UseCase().ExecuteAsync(Action(only.Id.Value));

        result.Status.Should().Be(EResultStatus.Success);
        _repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsNotFound_ForAnUnknownProvider()
    {
        var result = await UseCase().ExecuteAsync(Action(Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
    }
}
