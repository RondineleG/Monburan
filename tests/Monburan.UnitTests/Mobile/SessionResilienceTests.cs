using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Response;
using Monburan.Mobile.Shared;
using Monburan.Mobile.Shared.Api;
using Monburan.Mobile.Shared.Auth;
using Monburan.Mobile.Shared.Ui;

namespace Monburan.UnitTests.Mobile;

/// <summary>
/// Sessão do app com rede ruim e servidor instável: só a recusa do refresh token encerra a sessão. Queda de conexão,
/// tempo esgotado, 5xx e 429 são falhas temporárias e a pessoa continua logada.
/// </summary>
public class SessionResilienceTests
{
    private static readonly AuthSession Session = new("access-antigo", "refresh-valido");

    private static readonly object NewTokens = new { accessToken = "access-novo", refreshToken = "refresh-novo", erros = Array.Empty<string>() };

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Refresh_WhenServerIsUnavailable_KeepsSession(HttpStatusCode status)
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Refresh = _ => Respond(status);

        var outcome = await app.Tokens.RefreshAsync();

        outcome.Should().Be(RefreshOutcome.Unavailable);
        (await app.Tokens.GetSessionAsync()).Should().Be(Session);
        app.SessionExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(WebException))]
    [InlineData(typeof(SocketException))]
    public async Task Refresh_WhenConnectionFails_KeepsSession(Type failure)
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Refresh = _ => throw (Exception)Activator.CreateInstance(failure)!;

        var outcome = await app.Tokens.RefreshAsync();

        outcome.Should().Be(RefreshOutcome.Unavailable);
        (await app.Tokens.GetSessionAsync()).Should().Be(Session);
        app.SessionExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Refresh_WhenRefreshTokenIsRejected_EndsSession(HttpStatusCode status)
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Refresh = _ => Respond(status);

        var outcome = await app.Tokens.RefreshAsync();

        outcome.Should().Be(RefreshOutcome.Rejected);
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
        app.SessionExpired.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_WithEmptyTokens_EndsSession()
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Refresh = _ => Respond(HttpStatusCode.OK, new { erros = Array.Empty<string>() });

        var outcome = await app.Tokens.RefreshAsync();

        outcome.Should().Be(RefreshOutcome.Rejected);
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Refresh_WhenSignedOutWhileWaiting_DoesNotRestoreSession()
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Refresh = async _ =>
        {
            // A pessoa toca em "Sair" enquanto a renovação ainda está no ar
            await app.Tokens.ClearSessionAsync();
            return await Respond(HttpStatusCode.OK, NewTokens);
        };

        var outcome = await app.Tokens.RefreshAsync();

        outcome.Should().Be(RefreshOutcome.Rejected);
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ApiCall_WhenSessionCannotBeRenewedNow_FailsAsNetworkError_AndKeepsSession()
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Api = _ => Respond(HttpStatusCode.Unauthorized);
        app.Server.Refresh = _ => Respond(HttpStatusCode.ServiceUnavailable);

        var call = async () => await app.Api.GetMyCandidateAsync();

        var error = (await call.Should().ThrowAsync<ApiException>()).Which;
        error.IsNetworkError.Should().BeTrue("a tela não pode tratar uma falha temporária como sessão expirada");
        (await app.Tokens.GetSessionAsync()).Should().Be(Session);
        app.SessionExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(WebException))]
    [InlineData(typeof(SocketException))]
    public async Task ApiCall_WhenConnectionDropsMidRequest_BecomesNetworkError(Type failure)
    {
        // No Android, trocar de Wi-Fi para 4G no meio da requisição chega como WebException ou IOException
        var app = await FakeApp.SignedInAsync();
        app.Server.Api = _ => throw (Exception)Activator.CreateInstance(failure)!;

        var call = async () => await app.Api.GetMyApplicationsAsync();

        (await call.Should().ThrowAsync<ApiException>()).Which.IsNetworkError.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Initialize_WhenServerIsUnstable_StaysSignedIn(HttpStatusCode status)
    {
        // Abrir o app durante um deploy (502) não pode deslogar nem travar a área logada
        var app = await FakeApp.SignedInAsync();
        app.Server.Api = _ => Respond(status);

        await app.Auth.InitializeAsync();

        app.Auth.IsAuthenticated.Should().BeTrue();
        (await app.Tokens.GetSessionAsync()).Should().Be(Session);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Initialize_WhenAccountHasNoCandidateProfile_SignsOut(HttpStatusCode status)
    {
        var app = await FakeApp.SignedInAsync();
        app.Server.Api = _ => Respond(status);

        await app.Auth.InitializeAsync();

        app.Auth.IsAuthenticated.Should().BeFalse();
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Initialize_AfterUnexpectedFailure_TriesAgain()
    {
        var store = new InMemoryTokenStore { FailNextLoad = true };
        var app = await FakeApp.SignedInAsync(store);

        var first = async () => await app.Auth.InitializeAsync();
        await first.Should().ThrowAsync<InvalidOperationException>();

        await app.Auth.InitializeAsync();

        app.Auth.IsAuthenticated.Should().BeTrue();
        app.Auth.CurrentCandidate!.Email.Should().Be("pessoa@teste.com");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SignIn_WhenAccountHasNoCandidateProfile_IsRefused(HttpStatusCode status)
    {
        var app = FakeApp.Create(new InMemoryTokenStore());
        app.Server.Api = _ => Respond(status);

        var signIn = async () => await app.Auth.SignInAsync(new UserLoginResponse(true, "access", "refresh"));

        (await signIn.Should().ThrowAsync<ApiException>()).Which.Message.Should().Contain("para candidatos");
        app.Auth.IsAuthenticated.Should().BeFalse();
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public void Registration_GivesUploadsMoreTime_AndSharesTheBadgeState()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ITokenStore>(new InMemoryTokenStore())
            .AddMonburanApi(new Uri("https://api.exemplo.com/"))
            .BuildServiceProvider();
        var clients = provider.GetRequiredService<IHttpClientFactory>();

        clients.CreateClient(ApiClientNames.Upload).Timeout.Should().Be(TimeSpan.FromMinutes(3));
        clients.CreateClient(ApiClientNames.Api).Timeout.Should().Be(TimeSpan.FromSeconds(25));
        provider.GetRequiredService<NotificationBadgeState>()
            .Should().BeSameAs(provider.GetRequiredService<NotificationBadgeState>());
    }

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status, object? body = null) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body) });

    /// <summary>Os serviços do app (como no MauiProgram) apontando para um servidor falso.</summary>
    private sealed class FakeApp
    {
        public required FakeServer Server { get; init; }
        public required TokenService Tokens { get; init; }
        public required MonburanApiClient Api { get; init; }
        public required AuthStateService Auth { get; init; }
        public bool SessionExpired { get; private set; }

        public static FakeApp Create(InMemoryTokenStore store)
        {
            var server = new FakeServer();
            var clients = new FakeHttpClientFactory(server);
            var tokens = new TokenService(store, clients);
            clients.Tokens = tokens;
            var api = new MonburanApiClient(clients);

            var app = new FakeApp { Server = server, Tokens = tokens, Api = api, Auth = new AuthStateService(tokens, api) };
            tokens.SessionExpired += () => app.SessionExpired = true;
            return app;
        }

        public static async Task<FakeApp> SignedInAsync(InMemoryTokenStore? store = null)
        {
            store ??= new InMemoryTokenStore();
            await store.SaveAsync(Session);
            return Create(store);
        }
    }

    /// <summary>Servidor falso: a renovação (api/auth/refresh) e o resto da API respondem como cada teste definir.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Refresh { get; set; } = _ => Respond(HttpStatusCode.OK, NewTokens);

        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Api { get; set; } =
            _ => Respond(HttpStatusCode.OK, new CandidateResponse { Id = Guid.NewGuid(), Name = "Pessoa", Email = "pessoa@teste.com" });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath == "/api/auth/refresh" ? Refresh(request) : Api(request);
    }

    private sealed class FakeHttpClientFactory(FakeServer server) : IHttpClientFactory
    {
        public TokenService? Tokens { get; set; }

        public HttpClient CreateClient(string name)
        {
            HttpMessageHandler handler = name == ApiClientNames.Auth
                ? server
                : new AuthHeaderHandler(Tokens!) { InnerHandler = server };
            return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") };
        }
    }

    private sealed class InMemoryTokenStore : ITokenStore
    {
        private AuthSession? _session;

        /// <summary>Simula o armazenamento seguro do aparelho falhando uma vez (chaveiro bloqueado, por exemplo).</summary>
        public bool FailNextLoad { get; set; }

        public Task<AuthSession?> LoadAsync()
        {
            if (!FailNextLoad)
                return Task.FromResult(_session);

            FailNextLoad = false;
            throw new InvalidOperationException("Armazenamento seguro indisponível.");
        }

        public Task SaveAsync(AuthSession session)
        {
            _session = session;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _session = null;
            return Task.CompletedTask;
        }
    }
}
