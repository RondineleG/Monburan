using FluentAssertions;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

using NSubstitute;

namespace Monburan.UnitTests.Web;

public class AiTextClientTests
{
    private const string Key = "chave-secreta-de-teste";

    private readonly IAiProviderCatalog _catalog = Substitute.For<IAiProviderCatalog>();
    private readonly IAiSecretReader _secrets = Substitute.For<IAiSecretReader>();
    private readonly FakeProtocol _openAi = new(EAiProtocol.OpenAiCompatible);
    private readonly FakeProtocol _anthropic = new(EAiProtocol.Anthropic);

    private AiTextClient Client() => new(_catalog, _secrets, [_openAi, _anthropic]);

    private static AiCompletionRequest Request() => new([new AiMessage("user", "Oi")]);

    private void Configure(AiProviderSettings provider, bool keyIsSet = true, params AiProviderSettings[] others)
    {
        _catalog.Default.Returns(provider);
        _catalog.All.Returns(new[] { provider }.Concat(others).ToArray());
        _secrets.IsSet(Arg.Any<string>()).Returns(keyIsSet);
        _secrets.Get(Arg.Any<string>()).Returns(keyIsSet ? Key : null);
    }

    [Fact]
    public void IsConfigured_RequiresADefaultProviderWhoseKeyExistsOnTheServer()
    {
        var client = Client();
        client.IsConfigured.Should().BeFalse();
        client.ModelName.Should().BeNull();
        client.ProviderName.Should().Be("provedor de IA");

        Configure(AiTestData.OpenAi(), keyIsSet: false);
        client.IsConfigured.Should().BeFalse();
        client.ModelName.Should().BeNull();

        Configure(AiTestData.OpenAi(), keyIsSet: true);
        client.IsConfigured.Should().BeTrue();
        client.ProviderName.Should().Be("Provedor de teste");
        client.ModelName.Should().Be("modelo-de-teste");
    }

    [Fact]
    public async Task CompleteAsync_SendsThroughTheDefaultProviderWithItsKey()
    {
        var provider = AiTestData.OpenAi();
        Configure(provider);
        _openAi.Reply = new AiCompletion("pronto", "modelo-x", 3, 4);

        var completion = await Client().CompleteAsync(Request());

        completion.Content.Should().Be("pronto");
        _openAi.Provider.Should().Be(provider);
        _openAi.ApiKey.Should().Be(Key);
        _anthropic.Provider.Should().BeNull();
        _secrets.Received().Get("AI_KEY_TESTE");
    }

    [Fact]
    public async Task CompleteAsync_PicksTheProtocolTheProviderSpeaks()
    {
        var provider = AiTestData.Anthropic();
        Configure(provider);

        await Client().CompleteAsync(Request());

        _anthropic.Provider.Should().Be(provider);
        _openAi.Provider.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_WithoutADefaultProvider_FailsAsNotConfigured()
    {
        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.NotConfigured);
        _openAi.Provider.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_WithoutTheKey_FailsBeforeAnyNetworkCall_NamingTheVariableButNeverAValue()
    {
        Configure(AiTestData.OpenAi(), keyIsSet: false);

        var act = () => Client().CompleteAsync(Request());

        var exception = (await act.Should().ThrowAsync<AiClientException>()).Which;
        exception.Kind.Should().Be(AiFailureKind.NotConfigured);
        exception.Message.Should().Contain("AI_KEY_TESTE").And.NotContain(Key);
        _openAi.Provider.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_ByProviderId_UsesThatProviderEvenWhenDisabledOrNotTheDefault()
    {
        var standard = AiTestData.OpenAi();
        var other = AiTestData.Anthropic() with { Id = Guid.NewGuid(), Name = "Outro", IsDefault = false, IsEnabled = false, ApiKeyEnvVar = "AI_KEY_OUTRO" };
        Configure(standard, true, other);

        await Client().CompleteAsync(other.Id, Request());

        _anthropic.Provider.Should().Be(other);
        _openAi.Provider.Should().BeNull();
        _secrets.Received().Get("AI_KEY_OUTRO");
    }

    [Fact]
    public async Task CompleteAsync_ByUnknownProviderId_FailsAsNotConfigured()
    {
        Configure(AiTestData.OpenAi());

        var act = () => Client().CompleteAsync(Guid.NewGuid(), Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.NotConfigured);
    }

    [Fact]
    public async Task CompleteAsync_WithoutAnImplementationForTheProtocol_FailsAsNotConfigured()
    {
        Configure(AiTestData.Anthropic());
        var client = new AiTextClient(_catalog, _secrets, [_openAi]);

        var act = () => client.CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.NotConfigured);
    }

    private sealed class FakeProtocol(EAiProtocol protocol) : IAiProtocol
    {
        public EAiProtocol Protocol => protocol;

        public AiProviderSettings? Provider { get; private set; }

        public string? ApiKey { get; private set; }

        public AiCompletion Reply { get; set; } = new("resposta", "modelo", 1, 2);

        public Task<AiCompletion> SendAsync(
            AiProviderSettings provider, string apiKey, AiCompletionRequest request, CancellationToken cancellationToken)
        {
            Provider = provider;
            ApiKey = apiKey;
            return Task.FromResult(Reply);
        }
    }
}
