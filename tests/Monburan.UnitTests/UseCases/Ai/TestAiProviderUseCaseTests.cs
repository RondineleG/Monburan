using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Ai;
using Monburan.UnitTests.Web;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Ai;

public class TestAiProviderUseCaseTests
{
    private const string Key = "chave-secreta-de-teste-123";

    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiSecretReader _secrets = Substitute.For<IAiSecretReader>();
    private readonly IAiTextClient _ai = Substitute.For<IAiTextClient>();
    private readonly Monburan.Core.Entities.AiProvider _provider;

    public TestAiProviderUseCaseTests()
    {
        _provider = _repository.Add(AiTestData.Entity("Provedor", envVar: "AI_KEY_PROVEDOR"));
        _secrets.IsSet("AI_KEY_PROVEDOR").Returns(true);
        _secrets.Get("AI_KEY_PROVEDOR").Returns(Key);
    }

    private TestAiProviderUseCase UseCase() => new(_repository, _secrets, _ai);

    private AiProviderActionRequest Action() => new() { Id = _provider.Id.Value, UpdatedBy = "admin@monburan.com.br" };

    private void Replies(string content) =>
        _ai.CompleteAsync(Arg.Any<Guid>(), Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion(content, "modelo-do-provedor", 5, 2));

    private void Fails(AiFailureKind kind, string message = "detalhe técnico") =>
        _ai.CompleteAsync(Arg.Any<Guid>(), Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiCompletion>>(_ => throw new AiClientException(kind, message));

    [Fact]
    public async Task CallsTheChosenProvider_WithAMinimalPrompt_AndReportsTheReply()
    {
        Replies("ok");

        var result = await UseCase().ExecuteAsync(Action());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Success.Should().BeTrue();
        result.Data.Reply.Should().Be("ok");
        result.Data.Message.Should().Contain("respondeu");
        result.Data.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);

        await _ai.Received(1).CompleteAsync(
            _provider.Id.Value,
            Arg.Is<AiCompletionRequest>(request => request.Messages.Count == 1 && request.Messages[0].Role == "user" && !request.JsonObject),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrimsALongReplyToAShortExcerpt()
    {
        Replies(new string('x', 500));

        var result = await UseCase().ExecuteAsync(Action());

        result.Data!.Reply!.Length.Should().BeLessThan(100);
        result.Data.Reply.Should().EndWith("…");
    }

    [Fact]
    public async Task WhenTheVariableIsMissing_ExplainsAndNeverCallsTheProvider()
    {
        _secrets.IsSet("AI_KEY_PROVEDOR").Returns(false);

        var result = await UseCase().ExecuteAsync(Action());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Success.Should().BeFalse();
        result.Data.Message.Should().Contain("AI_KEY_PROVEDOR").And.Contain("não está definida");
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<Guid>(), Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AiFailureKind.NotConfigured, "não está configurado")]
    [InlineData(AiFailureKind.BlockedDestination, "rede interna ou reservada")]
    [InlineData(AiFailureKind.Timeout, "não respondeu a tempo")]
    [InlineData(AiFailureKind.RateLimited, "limitou as chamadas")]
    [InlineData(AiFailureKind.Unauthorized, "recusou a chave")]
    [InlineData(AiFailureKind.InvalidResponse, "formato esperado")]
    [InlineData(AiFailureKind.Truncated, "limite de tokens")]
    [InlineData(AiFailureKind.ProviderError, "O provedor falhou")]
    public async Task ExplainsEachFailureInPortuguese_WithoutLeakingTheKey(AiFailureKind kind, string expected)
    {
        Fails(kind, "O provedor de IA respondeu 500.");

        var result = await UseCase().ExecuteAsync(Action());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Success.Should().BeFalse();
        result.Data.Message.Should().Contain(expected).And.NotContain(Key);
        result.Data.Reply.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNotFound_ForAnUnknownProvider()
    {
        var result = await UseCase().ExecuteAsync(new AiProviderActionRequest { Id = Guid.NewGuid() });

        result.Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task LetsCancellationThrough_SoLeavingThePageStopsTheCall()
    {
        _ai.CompleteAsync(Arg.Any<Guid>(), Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiCompletion>>(_ => throw new OperationCanceledException());

        var act = () => UseCase().ExecuteAsync(Action(), new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
