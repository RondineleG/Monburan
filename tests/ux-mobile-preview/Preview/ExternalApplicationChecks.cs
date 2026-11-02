using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Services;
using Monburan.Mobile.Shared.Api;
using Monburan.Mobile.Shared.Auth;
using Monburan.Mobile.Shared.Ui;

namespace Monburan.Mobile.Preview;

/// <summary>Renderiza as telas reais com HTTP falso: vagas externas não podem pedir currículo nem candidatura interna.</summary>
public static class ExternalApplicationChecks
{
    public static async Task<int> RunAsync()
    {
        foreach (var (channel, expected) in new[]
        {
            (JobChannel.ExternalUrl, "https://empresa.example/vagas/1"),
            (JobChannel.WhatsApp, "https://wa.me/5547999998888"),
            (JobChannel.Email, "mailto:rh@empresa.example"),
            (JobChannel.OnSite, "Compareça à agência com seu currículo.")
        })
        {
            foreach (var (applyPage, authenticated) in new[] { (false, false), (false, true), (true, true) })
            {
                var job = new JobResponse
                {
                    Id = Guid.NewGuid(), Title = "Vaga externa", Status = "Active", PublicationType = JobChannel.External,
                    ApplicationChannel = channel, CompanyName = "Empresa contratante", ApplicationUrl = "https://empresa.example/vagas/1",
                    ApplicationContact = channel == JobChannel.Email ? "rh@empresa.example" : "(47) 99999-8888",
                    ApplicationInstructions = "Compareça à agência com seu currículo."
                };
                var services = new ServiceCollection().AddLogging();
                services.AddSingleton<IHttpClientFactory>(new FakeClients(job));
                services.AddSingleton<ITokenStore>(new FakeTokens(authenticated));
                services.AddSingleton<TokenService>();
                services.AddSingleton<MonburanApiClient>();
                services.AddSingleton<AuthStateService>();
                services.AddSingleton<IToastService, ToastService>();
                services.AddSingleton<NavigationManager>(new FakeNavigation(job.Id));
                await using var provider = services.BuildServiceProvider();
                await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
                var html = await renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { ["Id"] = job.Id });
                    var rendered = applyPage
                        ? await renderer.RenderComponentAsync<Components.Pages.Apply>(parameters)
                        : await renderer.RenderComponentAsync<Components.Pages.JobDetails>(parameters);
                    return WebUtility.HtmlDecode(rendered.ToHtmlString());
                });

                if (!html.Contains(expected) || !html.Contains("Empresa contratante") || !html.Contains("external-job-application")
                    || html.Contains("apply-resume") || html.Contains("Confirmar candidatura")
                    || (channel == JobChannel.OnSite && html.Contains("external-job-link")))
                    throw new InvalidOperationException($"Canal externo incorreto: {channel}, apply={applyPage}, authenticated={authenticated}");

                Console.WriteLine($"PASS: {channel}, apply={applyPage}, authenticated={authenticated}");
            }
        }
        return 0;
    }

    private sealed class FakeClients(JobResponse job) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FakeHttp(job)) { BaseAddress = new Uri("https://api.example/") };
    }

    private sealed class FakeHttp(JobResponse job) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object response = request.RequestUri!.AbsolutePath switch
            {
                var path when path == $"/api/v1/jobs/{job.Id}" => job,
                "/api/v1/candidates/me" => new CandidateResponse { Id = Guid.NewGuid(), Name = "Candidato" },
                _ => throw new InvalidOperationException($"Vaga externa fez chamada indevida: {request.Method} {request.RequestUri.AbsolutePath}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) });
        }
    }

    private sealed class FakeTokens(bool authenticated) : ITokenStore
    {
        public Task<AuthSession?> LoadAsync() => Task.FromResult(authenticated ? new AuthSession("access", "refresh") : null);
        public Task SaveAsync(AuthSession session) => Task.CompletedTask;
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation(Guid id) => Initialize("https://app.example/", $"https://app.example/apply/{id}");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException($"Redirecionamento indevido: {uri}");
    }
}
