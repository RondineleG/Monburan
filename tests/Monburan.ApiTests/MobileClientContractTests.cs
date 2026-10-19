using System.Net;
using System.Net.Http.Json;
using System.Text;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Mobile.Shared.Api;
using Monburan.Mobile.Shared.Auth;

namespace Monburan.ApiTests;

/// <summary>
/// Contrato real entre o app (Monburan.Mobile.Shared) e a API: os clientes tipados do app
/// conversam com o servidor de teste, validando rota, verbo e desserialização dos DTOs —
/// contrato quebrado silencioso só aparece assim, não lendo código.
/// </summary>
public class MobileClientContractTests : IClassFixture<MonburanApiFactory>
{
    private const string CandidateEmail = "joao.silva@email.com";
    private const string AdvertiserEmail = "contato@techsolutions.com";
    private const string Password = "Monburan123";

    private readonly MonburanApiFactory _factory;

    public MobileClientContractTests(MonburanApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Candidate_Journey_ThroughMobileClients_DeserializesEveryContract()
    {
        var published = await PublishJobAsync("Vaga do contrato mobile");

        var app = MobileApp.Create(_factory);
        await app.SignInAsync(CandidateEmail);

        app.Auth.IsAuthenticated.Should().BeTrue();
        app.Auth.CurrentCandidate!.Email.Should().Be(CandidateEmail);

        var jobs = await app.Api.GetJobsAsync(new ListJobsRequest { Page = 1, Size = 100, SortDescending = true });
        jobs.Jobs.Should().OnlyContain(j => j.Status == "Active", "a listagem pública só traz vagas ativas");
        var target = jobs.Jobs.Single(j => j.Id == published.Id);

        var mine = await app.Api.GetMyApplicationsAsync();
        mine.Should().OnlyContain(a => a.CandidateId == app.Auth.CurrentCandidate.Id);

        var detail = await app.Api.GetJobAsync(target.Id);
        detail.Title.Should().Be(target.Title);

        var created = await app.Api.ApplyAsync(target.Id, "Candidatura pelo app");
        created.JobId.Should().Be(target.Id);
        created.Status.Should().Be("Sent");

        (await app.Api.GetMyApplicationsAsync()).Should().Contain(a => a.JobId == target.Id);

        var duplicate = async () => await app.Api.ApplyAsync(target.Id);
        var error = (await duplicate.Should().ThrowAsync<ApiException>()).Which;
        error.StatusCode.Should().Be(HttpStatusCode.Conflict);
        error.Message.Should().Contain("já se candidatou");
    }

    [Fact]
    public async Task Resumes_CreateUploadAndDelete_ThroughMobileClient()
    {
        var app = MobileApp.Create(_factory);
        await app.SignInAsync("maria.santos@email.com");

        var created = await app.Api.CreateResumeAsync(new CreateResumeRequest
        {
            Title = "Currículo criado pelo app",
            Summary = "Resumo de teste do contrato mobile.",
            IsActive = false
        });
        created.Title.Should().Be("Currículo criado pelo app");

        (await app.Api.GetMyResumesAsync()).Should().Contain(r => r.Id == created.Id);
        (await app.Api.GetResumeAsync(created.Id)).Id.Should().Be(created.Id);

        await using var pdf = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n% conteúdo de teste"));
        var upload = await app.Api.UploadPdfAsync(pdf, "curriculo.pdf");
        upload.FileUrl.Should().StartWith("/uploads/").And.EndWith(".pdf");

        await app.Api.DeleteResumeAsync(created.Id);
        (await app.Api.GetMyResumesAsync()).Should().NotContain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task Profile_GetAndUpdate_ThroughMobileClient()
    {
        var app = MobileApp.Create(_factory);
        await app.SignInAsync(CandidateEmail);

        var profile = await app.Api.GetMyCandidateAsync();
        var updated = await app.Api.UpdateMyCandidateAsync(new UpdateCandidateRequest
        {
            Id = profile.Id,
            Name = profile.Name,
            Phone = "(11) 98888-7777",
            DateOfBirth = profile.DateOfBirth,
            LinkedIn = profile.LinkedIn
        });

        updated.Id.Should().Be(profile.Id);
        updated.Phone.Should().Be("(11) 98888-7777");
    }

    [Fact]
    public async Task StatusChange_CreatesInAppNotification_ForCandidate()
    {
        // Candidata-se a uma vaga nova da Tech Solutions e a empresa seleciona pela API
        var job = await PublishJobAsync("Vaga para notificação de status");
        var candidate = MobileApp.Create(_factory);
        await candidate.SignInAsync(CandidateEmail);
        var application = await candidate.Api.ApplyAsync(job.Id, "Quero participar");

        var advertiserClient = _factory.CreateClient();
        var advertiserToken = await LoginAsync(advertiserClient, AdvertiserEmail);
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/applications/{application.Id}/status")
        {
            Content = JsonContent.Create(new { newStatus = "Selected" })
        };
        update.Headers.Authorization = new("Bearer", advertiserToken);
        (await advertiserClient.SendAsync(update)).StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await candidate.Api.GetMyNotificationsAsync();
        notifications.Should().Contain(n => n.Title == "Você foi selecionado(a)!" && n.Message.Contains(job.Title));

        (await candidate.Api.GetUnreadCountAsync()).Should().BeGreaterThan(0);
        await candidate.Api.MarkAllNotificationsAsReadAsync();
        (await candidate.Api.GetUnreadCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpiredAccessToken_IsRenewedWithRefreshToken_AndRequestRetried()
    {
        var app = MobileApp.Create(_factory);
        var tokens = await app.AuthApi.LoginAsync(CandidateEmail, Password);

        // Access token inválido + refresh válido: o handler recebe 401, renova e repete 1×
        await app.Tokens.SetSessionAsync(new AuthSession("token-invalido", tokens.RefreshToken));

        var profile = await app.Api.GetMyCandidateAsync();

        profile.Email.Should().Be(CandidateEmail);
        var session = await app.Tokens.GetSessionAsync();
        session!.AccessToken.Should().NotBe("token-invalido");
    }

    [Fact]
    public async Task RejectedRefreshToken_ClearsSession_AndRaisesSessionExpired()
    {
        var app = MobileApp.Create(_factory);
        var expired = false;
        app.Tokens.SessionExpired += () => expired = true;
        await app.Tokens.SetSessionAsync(new AuthSession("token-invalido", "refresh-invalido"));

        var call = async () => await app.Api.GetMyCandidateAsync();

        var error = (await call.Should().ThrowAsync<ApiException>()).Which;
        error.IsUnauthorized.Should().BeTrue();
        error.Message.Should().NotContain("Unauthorized");
        expired.Should().BeTrue("o app volta para o login quando a renovação é rejeitada");
        (await app.Tokens.GetSessionAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsPortugueseMessages()
    {
        var app = MobileApp.Create(_factory);

        var register = async () => await app.AuthApi.RegisterAsync(new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Pessoa Teste",
            Email = $"fraca-{Guid.NewGuid():N}@teste.com",
            Password = "palavrasolta",
            ConfirmPassword = "palavrasolta"
        });

        var error = (await register.Should().ThrowAsync<ApiException>()).Which;
        error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Message.Should().Contain("número").And.Contain("maiúscula");
        error.Message.Should().NotContainAny("Passwords must", "One or more validation errors");
    }

    [Fact]
    public async Task Plans_AreReadableByMobileClient()
    {
        var app = MobileApp.Create(_factory);

        var plans = await app.Api.GetPlansAsync();

        plans.Plans.Should().Contain(p => p.Name == "Free");
        plans.CurrentSubscription.Should().BeNull("visitante sem login não tem assinatura");
    }

    [Fact]
    public async Task NonJsonResponse_BecomesApiException_InsteadOfCrashing()
    {
        // Proxy/manutenção devolvendo HTML com 200 — antes virava JsonException não tratada
        var factory = new StubHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Em manutenção</body></html>", Encoding.UTF8, "text/html")
        });
        var api = new MonburanApiClient(factory);

        var call = async () => await api.GetJobsAsync(new ListJobsRequest());

        var error = (await call.Should().ThrowAsync<ApiException>()).Which;
        error.IsNetworkError.Should().BeFalse();
        error.Message.Should().Contain("resposta inesperada");
    }

    private async Task<JobResponse> PublishJobAsync(string title)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, AdvertiserEmail);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(new CreateJobRequest
            {
                Title = title,
                Description = "Vaga publicada pelos testes de contrato do app.",
                ExpirationDate = DateTime.UtcNow.AddDays(15),
                NumberOfOpenings = 1,
                Type = "Remote",
                IsRemote = true
            })
        };
        request.Headers.Authorization = new("Bearer", token);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    /// <summary>Monta o grafo de serviços do app (como o MauiProgram) apontando para o TestServer.</summary>
    private sealed class MobileApp
    {
        public required TokenService Tokens { get; init; }
        public required MonburanApiClient Api { get; init; }
        public required AuthApiClient AuthApi { get; init; }
        public required AuthStateService Auth { get; init; }

        public static MobileApp Create(MonburanApiFactory factory)
        {
            var httpFactory = new TestServerHttpClientFactory(factory);
            var tokens = new TokenService(new InMemoryTokenStore(), httpFactory);
            httpFactory.Tokens = tokens;

            var api = new MonburanApiClient(httpFactory);
            return new MobileApp
            {
                Tokens = tokens,
                Api = api,
                AuthApi = new AuthApiClient(httpFactory),
                Auth = new AuthStateService(tokens, api)
            };
        }

        public async Task SignInAsync(string email)
        {
            var login = await AuthApi.LoginAsync(email, Password);
            await Auth.SignInAsync(login);
        }
    }

    private sealed class TestServerHttpClientFactory(MonburanApiFactory factory) : IHttpClientFactory
    {
        public TokenService? Tokens { get; set; }

        public HttpClient CreateClient(string name)
        {
            HttpMessageHandler handler = factory.Server.CreateHandler();
            if (name == ApiClientNames.Api)
                handler = new AuthHeaderHandler(Tokens!) { InnerHandler = handler };

            return new HttpClient(handler) { BaseAddress = factory.Server.BaseAddress };
        }
    }

    private sealed class StubHttpClientFactory(HttpResponseMessage response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(response)) { BaseAddress = new Uri("http://localhost/") };

        private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(response);
        }
    }

    private sealed class InMemoryTokenStore : ITokenStore
    {
        private AuthSession? _session;

        public Task<AuthSession?> LoadAsync() => Task.FromResult(_session);

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
