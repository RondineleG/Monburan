using System.Net;
using System.Text.Json;

using FluentAssertions;

using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class OpenAiProtocolTests
{
    private const string Key = "tp-chave-secreta-de-teste-123";

    private readonly AiStubHandler _handler = new();
    private readonly AiListLogger<OpenAiProtocol> _logger = new();
    private AiProviderSettings _provider = AiTestData.OpenAi();

    private Task<AiCompletion> SendAsync(AiCompletionRequest? request = null, CancellationToken cancellationToken = default) =>
        new OpenAiProtocol(new HttpClient(_handler), _logger).SendAsync(_provider, Key, request ?? Request(), cancellationToken);

    private static AiCompletionRequest Request(bool json = true) => new(
        [new AiMessage("system", "Regras."), new AiMessage("user", "CONTEUDO-DO-CURRICULO")],
        json);

    private static string Reply(string content, string finish = "stop") => JsonSerializer.Serialize(new
    {
        id = "abc",
        model = "modelo-do-provedor",
        choices = new[] { new { index = 0, finish_reason = finish, message = new { role = "assistant", content } } },
        usage = new { prompt_tokens = 17, completion_tokens = 746 }
    });

    [Fact]
    public async Task SendAsync_PostsAnOpenAiChatCompletionRequest()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("{}"));

        await SendAsync();

        _handler.Request!.Method.Should().Be(HttpMethod.Post);
        _handler.Request.RequestUri.Should().Be(new Uri("https://api.exemplo.com/v1/chat/completions"));
        _handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        _handler.Request.Headers.Authorization.Parameter.Should().Be(Key);
        _handler.Request.Headers.Contains("x-api-key").Should().BeFalse();

        using var body = JsonDocument.Parse(_handler.Body!);
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("modelo-de-teste");
        root.GetProperty("temperature").GetDouble().Should().Be(0.4);
        root.GetProperty("max_tokens").GetInt32().Should().Be(4000);
        root.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().HaveCount(2);
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[1].GetProperty("content").GetString().Should().Be("CONTEUDO-DO-CURRICULO");
    }

    [Theory]
    [InlineData("https://api.exemplo.com/v1", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("https://api.exemplo.com/v1/", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("  https://api.exemplo.com/v1//  ", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("https://openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("https://api.deepseek.com", "https://api.deepseek.com/chat/completions")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    public void Endpoint_AppendsThePathToTheBaseUrl(string baseUrl, string expected) =>
        OpenAiProtocol.Endpoint(baseUrl).Should().Be(new Uri(expected));

    [Fact]
    public async Task SendAsync_OmitsResponseFormat_WhenNotRequestedOrTurnedOff()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("texto"));

        await SendAsync(Request(json: false));
        _handler.Body.Should().NotContain("response_format");

        _provider = _provider with { UseJsonResponseFormat = false };
        await SendAsync(Request(json: true));
        _handler.Body.Should().NotContain("response_format");
    }

    [Fact]
    public async Task SendAsync_UsesMaxCompletionTokens_ForModelsThatRefuseMaxTokens()
    {
        _provider = _provider with { UseMaxCompletionTokens = true };
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await SendAsync();

        using var body = JsonDocument.Parse(_handler.Body!);
        body.RootElement.GetProperty("max_completion_tokens").GetInt32().Should().Be(4000);
        body.RootElement.TryGetProperty("max_tokens", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_OmitsTemperature_WhenTurnedOff()
    {
        _provider = _provider with { SendTemperature = false };
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await SendAsync();

        using var body = JsonDocument.Parse(_handler.Body!);
        body.RootElement.TryGetProperty("temperature", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ClampsOutOfRangeSettings()
    {
        _provider = _provider with { MaxOutputTokens = 99_999_999, Temperature = 9 };
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await SendAsync();

        using var high = JsonDocument.Parse(_handler.Body!);
        high.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(32000);
        high.RootElement.GetProperty("temperature").GetDouble().Should().Be(2);

        _provider = _provider with { MaxOutputTokens = 1, Temperature = -3 };
        await SendAsync();

        using var low = JsonDocument.Parse(_handler.Body!);
        low.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(256);
        low.RootElement.GetProperty("temperature").GetDouble().Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_ReturnsTheTextModelAndUsage()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("  {\"summary\":\"ok\"}  "));

        var completion = await SendAsync();

        completion.Content.Should().Be("{\"summary\":\"ok\"}");
        completion.Model.Should().Be("modelo-do-provedor");
        completion.PromptTokens.Should().Be(17);
        completion.CompletionTokens.Should().Be(746);
    }

    [Fact]
    public async Task SendAsync_RemovesTheReasoningBlockSomeServersPutInTheText()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("<think>vou pensar\nem várias linhas</think>\n{\"summary\":\"ok\"}"));

        var completion = await SendAsync();

        completion.Content.Should().Be("{\"summary\":\"ok\"}");
    }

    [Fact]
    public async Task SendAsync_JoinsTextPartsWhenContentIsAList()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"model":"m","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":[{"type":"text","text":"Olá, "},{"type":"text","text":"mundo"}]}}]}
            """);

        var completion = await SendAsync(Request(json: false));

        completion.Content.Should().Be("Olá, mundo");
        completion.PromptTokens.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AiFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, AiFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.RequestTimeout, AiFailureKind.Timeout)]
    [InlineData(HttpStatusCode.GatewayTimeout, AiFailureKind.Timeout)]
    [InlineData(HttpStatusCode.BadRequest, AiFailureKind.ProviderError)]
    [InlineData(HttpStatusCode.PaymentRequired, AiFailureKind.ProviderError)]
    [InlineData(HttpStatusCode.InternalServerError, AiFailureKind.ProviderError)]
    public async Task SendAsync_MapsHttpFailures(HttpStatusCode status, AiFailureKind expected)
    {
        _handler.Respond(status, "{\"error\":{\"message\":\"detalhe do provedor\"}}");

        var act = () => SendAsync();

        var exception = (await act.Should().ThrowAsync<AiClientException>()).Which;
        exception.Kind.Should().Be(expected);
        exception.Message.Should().NotContain(Key);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
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
            {"choices":[{"finish_reason":"length","message":{"role":"assistant","content":"","reasoning_content":"pensando..."}}]}
            """);

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Truncated);
    }

    [Fact]
    public async Task SendAsync_MapsATimeoutNotCausedByTheCaller()
    {
        _handler.Throw(new TaskCanceledException("tempo esgotado"));

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Timeout);
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
    public async Task SendAsync_MapsNetworkFailures()
    {
        _handler.Throw(new HttpRequestException("sem rota"));

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
    }

    [Fact]
    public async Task SendAsync_MapsABlockedDestination()
    {
        _handler.Throw(new HttpRequestException("falha", new AiBlockedDestinationException("api.exemplo.com")));

        var act = () => SendAsync();

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.BlockedDestination);
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
        _logger.Messages.Should().OnlyContain(message => message.Contains("Provedor de teste") && message.Contains("api.exemplo.com"));
    }
}
