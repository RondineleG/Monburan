using System.Net;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class OpenAiCompatibleClientTests
{
    private const string Key = "tp-chave-secreta-de-teste-123";

    private readonly AiOptions _options = new()
    {
        BaseUrl = "https://api.exemplo.com/v1/",
        ApiKey = Key,
        Model = "modelo-de-teste",
        MaxOutputTokens = 4000,
        Temperature = 0.4
    };

    private readonly StubHandler _handler = new();
    private readonly ListLogger _logger = new();

    private OpenAiCompatibleClient Client() =>
        new(new HttpClient(_handler), new StaticOptionsMonitor<AiOptions>(_options), _logger);

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
    public async Task CompleteAsync_PostsAnOpenAiChatCompletionRequest()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("{}"));

        await Client().CompleteAsync(Request());

        _handler.Request!.Method.Should().Be(HttpMethod.Post);
        _handler.Request.RequestUri.Should().Be(new Uri("https://api.exemplo.com/v1/chat/completions"));
        _handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        _handler.Request.Headers.Authorization.Parameter.Should().Be(Key);

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

    [Fact]
    public async Task CompleteAsync_OmitsResponseFormat_WhenNotRequestedOrTurnedOff()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("texto"));

        await Client().CompleteAsync(Request(json: false));
        _handler.Body.Should().NotContain("response_format");

        _options.UseJsonResponseFormat = false;
        await Client().CompleteAsync(Request(json: true));
        _handler.Body.Should().NotContain("response_format");
    }

    [Fact]
    public async Task CompleteAsync_ClampsOutOfRangeSettings()
    {
        _options.MaxOutputTokens = 99_999_999;
        _options.Temperature = 9;
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        await Client().CompleteAsync(Request());

        using var body = JsonDocument.Parse(_handler.Body!);
        body.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(16000);
        body.RootElement.GetProperty("temperature").GetDouble().Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsTheTextModelAndUsage()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("  {\"summary\":\"ok\"}  "));

        var completion = await Client().CompleteAsync(Request());

        completion.Content.Should().Be("{\"summary\":\"ok\"}");
        completion.Model.Should().Be("modelo-do-provedor");
        completion.PromptTokens.Should().Be(17);
        completion.CompletionTokens.Should().Be(746);
    }

    [Fact]
    public async Task CompleteAsync_RemovesTheReasoningBlockSomeServersPutInTheText()
    {
        _handler.Respond(HttpStatusCode.OK, Reply("<think>vou pensar\nem várias linhas</think>\n{\"summary\":\"ok\"}"));

        var completion = await Client().CompleteAsync(Request());

        completion.Content.Should().Be("{\"summary\":\"ok\"}");
    }

    [Fact]
    public async Task CompleteAsync_JoinsTextPartsWhenContentIsAList()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"model":"m","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":[{"type":"text","text":"Olá, "},{"type":"text","text":"mundo"}]}}]}
            """);

        var completion = await Client().CompleteAsync(Request(json: false));

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
    [InlineData(HttpStatusCode.InternalServerError, AiFailureKind.ProviderError)]
    public async Task CompleteAsync_MapsHttpFailures(HttpStatusCode status, AiFailureKind expected)
    {
        _handler.Respond(status, "{\"error\":{\"message\":\"detalhe do provedor\"}}");

        var act = () => Client().CompleteAsync(Request());

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
    public async Task CompleteAsync_RejectsInvalidReplies(string body)
    {
        _handler.Respond(HttpStatusCode.OK, body);

        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.InvalidResponse);
    }

    [Fact]
    public async Task CompleteAsync_ReportsTruncation_WhenTheTokenLimitEndedBeforeTheAnswer()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"choices":[{"finish_reason":"length","message":{"role":"assistant","content":"","reasoning_content":"pensando..."}}]}
            """);

        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Truncated);
    }

    [Fact]
    public async Task CompleteAsync_MapsATimeoutNotCausedByTheCaller()
    {
        _handler.Throw(new TaskCanceledException("tempo esgotado"));

        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.Timeout);
    }

    [Fact]
    public async Task CompleteAsync_LetsCallerCancellationThrough()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Respond(HttpStatusCode.OK, Reply("ok"));

        var act = () => Client().CompleteAsync(Request(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CompleteAsync_MapsNetworkFailures()
    {
        _handler.Throw(new HttpRequestException("sem rota"));

        var act = () => Client().CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.ProviderError);
    }

    [Theory]
    [InlineData("", "modelo", "https://api.exemplo.com/v1")]
    [InlineData("chave", "", "https://api.exemplo.com/v1")]
    [InlineData("chave", "modelo", "http://api.exemplo.com/v1")]
    [InlineData("chave", "modelo", "")]
    public async Task CompleteAsync_WhenNotConfigured_FailsBeforeAnyNetworkCall(string key, string model, string baseUrl)
    {
        _options.ApiKey = key;
        _options.Model = model;
        _options.BaseUrl = baseUrl;

        var client = Client();
        client.IsConfigured.Should().BeFalse();
        client.ModelName.Should().BeNull();

        var act = () => client.CompleteAsync(Request());

        (await act.Should().ThrowAsync<AiClientException>()).Which.Kind.Should().Be(AiFailureKind.NotConfigured);
        _handler.Request.Should().BeNull();
    }

    [Fact]
    public void Properties_ReflectTheConfiguration()
    {
        _options.ProviderName = "Xiaomi MiMo";

        var client = Client();

        client.IsConfigured.Should().BeTrue();
        client.ProviderName.Should().Be("Xiaomi MiMo");
        client.ModelName.Should().Be("modelo-de-teste");
    }

    [Fact]
    public async Task CompleteAsync_NeverLogsTheKeyOrTheResumeText()
    {
        _handler.Respond(HttpStatusCode.BadGateway, "falha no servidor do provedor");
        await Client().Invoking(client => client.CompleteAsync(Request())).Should().ThrowAsync<AiClientException>();

        _handler.Respond(HttpStatusCode.OK, Reply("ok"));
        await Client().CompleteAsync(Request());

        _logger.Messages.Should().NotBeEmpty();
        _logger.Messages.Should().NotContain(message => message.Contains(Key));
        _logger.Messages.Should().NotContain(message => message.Contains("CONTEUDO-DO-CURRICULO"));
        _logger.Messages.Should().Contain(message => message.Contains("502"));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _content = "{}";
        private Exception? _failure;

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public void Respond(HttpStatusCode status, string content)
        {
            _status = status;
            _content = content;
            _failure = null;
        }

        public void Throw(Exception failure) => _failure = failure;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_failure is not null)
                throw _failure;

            return new HttpResponseMessage(_status) { Content = new StringContent(_content, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ListLogger : ILogger<OpenAiCompatibleClient>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
    }
}
