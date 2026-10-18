using System.Net;
using System.Text.Json;

using FluentAssertions;

using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AnthropicProtocolTests
{
    private const string Key = "chave-secreta-de-teste-anthropic-123";

    private readonly AiStubHandler _handler = new();
    private readonly AiListLogger<AnthropicProtocol> _logger = new();
    private AiProviderSettings _provider = AiTestData.Anthropic();

    private Task<AiCompletion> SendAsync(AiCompletionRequest? request = null, CancellationToken cancellationToken = default) =>
        new AnthropicProtocol(new HttpClient(_handler), _logger).SendAsync(_provider, Key, request ?? Request(), cancellationToken);

    private static AiCompletionRequest Request() => new(
        [new AiMessage("system", "Regras."), new AiMessage("user", "CONTEUDO-DO-CURRICULO")],
        JsonObject: true);

    private static string Reply(string text, string stop = "end_turn") => JsonSerializer.Serialize(new
    {
        id = "msg_1",
        type = "message",
        role = "assistant",
        model = "modelo-do-provedor",
        content = new object[] { new { type = "text", text } },
        stop_reason = stop,
        usage = new { input_tokens = 21, output_tokens = 55 }
    });

    [Fact]
    public async Task SendAsync_PostsAnAnthropicMessagesRequest()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("{}"));

        await SendAsync();

        _handler.Request!.Method.Should().Be(HttpMethod.Post);
        _handler.Request.RequestUri.Should().Be(new Uri("https://api.exemplo.com/v1/messages"));
        _handler.Request.Headers.GetValues("x-api-key").Should().Equal(Key);
        _handler.Request.Headers.GetValues("anthropic-version").Should().Equal("2023-06-01");
        _handler.Request.Headers.Authorization.Should().BeNull();

        using var body = JsonDocument.Parse(_handler.Body!);
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("modelo-de-teste");
        root.GetProperty("max_tokens").GetInt32().Should().Be(4000);
        root.GetProperty("temperature").GetDouble().Should().Be(0.4);
        root.GetProperty("system").GetString().Should().Be("Regras.");
        root.TryGetProperty("response_format", out _).Should().BeFalse();
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().ContainSingle();
        messages[0].GetProperty("role").GetString().Should().Be("user");
        messages[0].GetProperty("content").GetString().Should().Be("CONTEUDO-DO-CURRICULO");
    }

    [Fact]
    public async Task SendAsync_MovesSystemMessagesToTheSystemFieldAndKeepsTheRestInOrder()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));
        var request = new AiCompletionRequest(
        [
            new AiMessage("system", "Primeira regra."),
            new AiMessage("user", "Pergunta 1"),
            new AiMessage("assistant", "Resposta 1"),
            new AiMessage("user", "Pergunta 2"),
            new AiMessage("system", "Segunda regra.")
        ]);

        await SendAsync(request);

        using var body = JsonDocument.Parse(_handler.Body!);
        body.RootElement.GetProperty("system").GetString().Should().Be("Primeira regra.\n\nSegunda regra.");
        body.RootElement.GetProperty("messages").EnumerateArray()
            .Select(message => message.GetProperty("role").GetString() + ":" + message.GetProperty("content").GetString())
            .Should().Equal("user:Pergunta 1", "assistant:Resposta 1", "user:Pergunta 2");
    }

    [Fact]
    public async Task SendAsync_OmitsTheSystemField_WhenThereAreNoInstructions()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await SendAsync(new AiCompletionRequest([new AiMessage("user", "Oi")]));

        using var body = JsonDocument.Parse(_handler.Body!);
        body.RootElement.TryGetProperty("system", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://api.anthropic.com", "https://api.anthropic.com/v1/messages")]
    [InlineData("https://api.anthropic.com/", "https://api.anthropic.com/v1/messages")]
    [InlineData("https://api.anthropic.com/v1", "https://api.anthropic.com/v1/messages")]
    [InlineData("https://api.anthropic.com/V1/", "https://api.anthropic.com/V1/messages")]
    [InlineData("https://token-plan-sgp.xiaomimimo.com/anthropic", "https://token-plan-sgp.xiaomimimo.com/anthropic/v1/messages")]
    [InlineData("  https://gateway.exemplo.com/claude//  ", "https://gateway.exemplo.com/claude/v1/messages")]
    public void Endpoint_AddsV1MessagesUnlessTheAddressAlreadyEndsInV1(string baseUrl, string expected) =>
        AnthropicProtocol.Endpoint(baseUrl).Should().Be(new Uri(expected));

    [Fact]
    public async Task SendAsync_ClampsTemperatureToOne_AndOmitsItWhenTurnedOff()
    {
        _provider = _provider with { Temperature = 1.7 };
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await SendAsync();

        using (var clamped = JsonDocument.Parse(_handler.Body!))
            clamped.RootElement.GetProperty("temperature").GetDouble().Should().Be(1);

        _provider = _provider with { SendTemperature = false };
        await SendAsync();

        using var omitted = JsonDocument.Parse(_handler.Body!);
        omitted.RootElement.TryGetProperty("temperature", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ReturnsTheTextModelAndUsage()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("  {\"summary\":\"ok\"}  "));

        var completion = await SendAsync();

        completion.Content.Should().Be("{\"summary\":\"ok\"}");
        completion.Model.Should().Be("modelo-do-provedor");
        completion.PromptTokens.Should().Be(21);
        completion.CompletionTokens.Should().Be(55);
    }

    [Fact]
    public async Task SendAsync_JoinsTextBlocksAndIgnoresThinkingBlocks()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"model":"m","stop_reason":"end_turn","content":[
              {"type":"thinking","thinking":"raciocínio interno que não pode vazar"},
              {"type":"text","text":"Olá, "},
              {"type":"text","text":"mundo"}]}
            """);

        var completion = await SendAsync();

        completion.Content.Should().Be("Olá, mundo");
        completion.PromptTokens.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AiFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, AiFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.GatewayTimeout, AiFailureKind.Timeout)]
    [InlineData(HttpStatusCode.BadRequest, AiFailureKind.ProviderError)]
    [InlineData(HttpStatusCode.InternalServerError, AiFailureKind.ProviderError)]
    [InlineData((HttpStatusCode)529, AiFailureKind.ProviderError)]
    public async Task SendAsync_MapsHttpFailures(HttpStatusCode status, AiFailureKind expected)
    {
        _handler.Respond(status, "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"detalhe do provedor\"}}");

        var act = () => SendAsync();

        var exception = (await act.Should().ThrowAsync<AiClientException>()).Which;
        exception.Kind.Should().Be(expected);
        exception.Message.Should().NotContain(Key);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{}")]
    [InlineData("{\"content\":[]}")]
    [InlineData("{\"content\":\"texto solto\"}")]
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"\"}]}")]
    [InlineData("{\"content\":[{\"type\":\"tool_use\",\"name\":\"x\"}]}")]
    public async Task SendAsync_RejectsInvalidReplies(string body)
    {
        _handler.Respond(HttpStatusCode.OK, body);

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.InvalidResponse);
    }

    [Fact]
    public async Task SendAsync_ReportsTruncation_WhenTheTokenLimitEndedBeforeTheAnswer()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"stop_reason":"max_tokens","content":[{"type":"thinking","thinking":"pensando..."}]}
            """);

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Truncated);
    }

    [Fact]
    public async Task SendAsync_MapsTimeoutNetworkAndBlockedDestinationFailures()
    {
        _handler.Throw(new TaskCanceledException("tempo esgotado"));
        (await ((Func<Task>)(() => SendAsync())).Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Timeout);

        _handler.Throw(new HttpRequestException("sem rota"));
        (await ((Func<Task>)(() => SendAsync())).Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);

        _handler.Throw(new HttpRequestException("falha", new AiBlockedDestinationException("api.exemplo.com")));
        (await ((Func<Task>)(() => SendAsync())).Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.BlockedDestination);
    }

    [Fact]
    public async Task SendAsync_LetsCallerCancellationThrough()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        var act = () => SendAsync(cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_NeverLogsTheKeyOrTheResumeText()
    {
        _handler.Respond(HttpStatusCode.BadGateway, "falha no servidor do provedor");
        await ((Func<Task>)(() => SendAsync())).Should().ThrowAsync<AiClientException>();

        _handler.Respond(HttpStatusCode.OK, Reply("ok"));
        await SendAsync();

        _logger.Messages.Should().NotBeEmpty();
        _logger.Messages.Should().NotContain(message => message.Contains(Key));
        _logger.Messages.Should().NotContain(message => message.Contains("CONTEUDO-DO-CURRICULO"));
        _logger.Messages.Should().Contain(message => message.Contains("502"));
    }
}
