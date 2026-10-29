using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>
/// Tela do administrador "Provedores de IA": cadastrar, testar, escolher o padrão, editar, desativar e excluir, tudo pela
/// interface. O provedor de IA é um servidor falso na própria máquina (o app em Development aceita endereços locais) e a
/// chave dele é um valor falso de <c>appsettings.Development.json</c>: nenhum teste fala com um serviço de verdade.
/// </summary>
public class AdminAiProvidersTests : E2ETestBase
{
    private const string AdminEmail = "admin@monburan.com.br";
    private const string FakeKey = "valor-falso-so-para-o-teste-e2e";
    private const string PagePath = "/admin/provedores-ia";

    public AdminAiProvidersTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private ILocator Card(string name) =>
        Page.Locator($"[data-testid='ai-provider-card'][data-provider-name='{name}']");

    private static string UniqueName() => "E2E " + Guid.NewGuid().ToString("N")[..8];

    private async Task LoginAsAdminAsync() =>
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword), "O administrador de desenvolvimento deveria conseguir entrar");

    private async Task OpenProvidersPageAsync()
    {
        await GotoAsync(PagePath);
        await WaitForBlazorAsync();
        await Page.GetByTestId("ai-status").WaitForAsync();
    }

    private async Task<string?> DefaultProviderNameAsync()
    {
        var card = Page.Locator("[data-testid='ai-provider-card']:has(.ai-badge--gold)").First;
        return await card.CountAsync() == 0 ? null : await card.GetAttributeAsync("data-provider-name");
    }

    private async Task FillFormAsync(string name, string url, string model, string envSuffix)
    {
        await Page.GetByTestId("ai-form-name").FillAsync(name);
        await Page.GetByTestId("ai-form-url").FillAsync(url);
        await Page.GetByTestId("ai-form-model").FillAsync(model);
        await Page.GetByTestId("ai-form-env").FillAsync(envSuffix);
    }

    private async Task AddProviderAsync(string name, string url, string model, string envSuffix)
    {
        await Page.GetByTestId("ai-provider-new").ClickAsync();
        await Page.GetByTestId("ai-provider-form").WaitForAsync();
        await FillFormAsync(name, url, model, envSuffix);
        await Page.GetByTestId("ai-form-save").ClickAsync();
        await Card(name).WaitForAsync();
    }

    private async Task DeleteProviderAsync(string name)
    {
        await Card(name).GetByTestId("ai-provider-delete").ClickAsync();
        await Page.GetByTestId("confirm-dialog").WaitForAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await Card(name).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    /// <summary>Devolve o padrão original e remove o que o teste criou, mesmo quando uma verificação falhou no meio.</summary>
    private async Task CleanUpAsync(string name, string? originalDefault)
    {
        try
        {
            await OpenProvidersPageAsync();

            if (originalDefault is not null
                && await Card(originalDefault).GetByText("Padrão", new LocatorGetByTextOptions { Exact = true }).CountAsync() == 0)
            {
                await Card(originalDefault).GetByTestId("ai-provider-default").ClickAsync();
                await Assertions.Expect(Card(originalDefault)).ToContainTextAsync("Padrão");
            }

            if (await Card(name).CountAsync() > 0)
                await DeleteProviderAsync(name);
        }
        catch (PlaywrightException)
        {
            // Limpeza de melhor esforço: a falha de verdade já está no teste
        }
    }

    [Fact]
    public async Task Admin_SeesTheMenuLink_AndTheSeededProviders_WithTheDefaultMarked()
    {
        await LoginAsAdminAsync();
        await GotoAsync("/");
        await WaitForBlazorAsync();

        var link = Page.GetByTestId("nav-admin-ai-providers");
        await Assertions.Expect(link).ToBeVisibleAsync();
        await link.ClickAsync();
        await Page.WaitForURLAsync(new Regex(Regex.Escape(PagePath), RegexOptions.IgnoreCase));
        await Page.GetByTestId("ai-status").WaitForAsync();

        // Aguarda o circuito interativo terminar de recarregar a lista (o SSR sozinho pode ser substituído durante a navegação)
        await Assertions.Expect(Page.GetByTestId("ai-provider-card")).Not.ToHaveCountAsync(0);
        Assert.True(await Page.GetByTestId("ai-provider-card").CountAsync() > 0, "Deveria haver provedores cadastrados");
        Assert.True(await Page.Locator("[data-testid='ai-provider-card'] .ai-badge--gold").CountAsync() <= 1,
            "No máximo um provedor pode ser o padrão");
    }

    [Fact]
    public async Task TheScreen_NeverShowsAnyKeyValue_OnlyTheVariableName()
    {
        var server = await FakeOpenAiServer.StartAsync();
        await using var _ = server;
        var name = UniqueName();
        await LoginAsAdminAsync();
        await OpenProvidersPageAsync();
        var originalDefault = await DefaultProviderNameAsync();

        try
        {
            await AddProviderAsync(name, server.BaseUrl + "/v1", "modelo-e2e", "e2e_fake");

            var text = await Page.Locator("body").InnerTextAsync();
            var html = await Page.ContentAsync();
            Assert.DoesNotContain(FakeKey, text);
            Assert.DoesNotContain(FakeKey, html);
            Assert.Contains("AI_KEY_E2E_FAKE", text);
            Assert.Contains("definida no servidor", await Card(name).InnerTextAsync());
        }
        finally
        {
            await CleanUpAsync(name, originalDefault);
        }
    }

    [Fact]
    public async Task Admin_AddsTestsPromotesEditsDisablesAndDeletesAProvider()
    {
        var server = await FakeOpenAiServer.StartAsync();
        await using var _ = server;
        var name = UniqueName();
        await LoginAsAdminAsync();
        await OpenProvidersPageAsync();
        var originalDefault = await DefaultProviderNameAsync();

        try
        {
            // Cadastrar: o endereço, o modelo e o NOME da variável (a chave vem do ambiente do servidor)
            await AddProviderAsync(name, server.BaseUrl + "/v1", "modelo-e2e", "e2e_fake");
            await Assertions.Expect(Card(name)).ToContainTextAsync("modelo-e2e");
            await Assertions.Expect(Card(name)).ToContainTextAsync("AI_KEY_E2E_FAKE");
            await Assertions.Expect(Card(name)).ToContainTextAsync("OpenAI-compatível");

            // Testar: chamada de verdade ao servidor falso, com a chave do ambiente no cabeçalho
            await Card(name).GetByTestId("ai-provider-test").ClickAsync();
            var result = Card(name).GetByTestId("ai-test-result");
            await Assertions.Expect(result).ToContainTextAsync("Funcionou");
            await Assertions.Expect(result).ToContainTextAsync("ok");
            Assert.Equal(1, server.Calls);
            Assert.Equal($"Bearer {FakeKey}", server.LastAuthorization);

            // Tornar padrão: a marca sai do antigo e a faixa de situação cita o novo
            await Card(name).GetByTestId("ai-provider-default").ClickAsync();
            await Assertions.Expect(Card(name)).ToContainTextAsync("Padrão");
            await Assertions.Expect(Page.GetByTestId("ai-status")).ToContainTextAsync(name);
            Assert.Equal(1, await Page.Locator("[data-testid='ai-provider-card'] .ai-badge--gold").CountAsync());
            if (originalDefault is not null)
                await Assertions.Expect(Card(originalDefault).GetByText("Padrão", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);

            // O padrão não pode ser excluído nem desativado enquanto houver outros
            await Assertions.Expect(Card(name).GetByTestId("ai-provider-toggle")).ToHaveCountAsync(0);
            await Card(name).GetByTestId("ai-provider-delete").ClickAsync();
            await Page.GetByTestId("confirm-accept").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("toast").Filter(new LocatorFilterOptions { HasText = "outro provedor como padrão" })).ToBeVisibleAsync();
            await Assertions.Expect(Card(name)).ToBeVisibleAsync();

            // Devolve o padrão original para poder desativar e excluir o provedor do teste
            if (originalDefault is not null)
            {
                await Card(originalDefault).GetByTestId("ai-provider-default").ClickAsync();
                await Assertions.Expect(Card(originalDefault)).ToContainTextAsync("Padrão");
            }

            // Editar
            await Card(name).GetByTestId("ai-provider-edit").ClickAsync();
            await Page.GetByTestId("ai-form-model").FillAsync("modelo-e2e-2");
            await Page.GetByTestId("ai-form-save").ClickAsync();
            await Assertions.Expect(Card(name)).ToContainTextAsync("modelo-e2e-2");

            // Desativar e ativar de novo
            await Card(name).GetByTestId("ai-provider-toggle").ClickAsync();
            await Assertions.Expect(Card(name)).ToContainTextAsync("Desativado");
            await Assertions.Expect(Card(name).GetByTestId("ai-provider-default")).ToHaveCountAsync(0);
            await Card(name).GetByTestId("ai-provider-toggle").ClickAsync();
            await Assertions.Expect(Card(name)).ToContainTextAsync("Ativo");

            // Excluir
            await DeleteProviderAsync(name);
            await Assertions.Expect(Card(name)).ToHaveCountAsync(0);
        }
        finally
        {
            await CleanUpAsync(name, originalDefault);
        }
    }

    [Fact]
    public async Task Test_ExplainsWhenTheKeyVariableIsNotDefinedOnTheServer()
    {
        var name = UniqueName();
        await LoginAsAdminAsync();
        await OpenProvidersPageAsync();
        var originalDefault = await DefaultProviderNameAsync();

        try
        {
            await AddProviderAsync(name, "https://api.exemplo.com/v1", "modelo-e2e", "E2E_SEM_CHAVE_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant());
            await Assertions.Expect(Card(name).GetByTestId("ai-key-status")).ToContainTextAsync("não definida");

            await Card(name).GetByTestId("ai-provider-test").ClickAsync();

            var result = Card(name).GetByTestId("ai-test-result");
            await Assertions.Expect(result).ToContainTextAsync("Não funcionou");
            await Assertions.Expect(result).ToContainTextAsync("não está definida neste servidor");
        }
        finally
        {
            await CleanUpAsync(name, originalDefault);
        }
    }

    [Fact]
    public async Task Form_ShowsTheServerErrorsNextToTheFields_AndSavesNothing()
    {
        var name = UniqueName();
        await LoginAsAdminAsync();
        await OpenProvidersPageAsync();
        var before = await Page.GetByTestId("ai-provider-card").CountAsync();

        await Page.GetByTestId("ai-provider-new").ClickAsync();
        await Page.GetByTestId("ai-provider-form").WaitForAsync();
        await FillFormAsync(name, "http://api.exemplo.com/v1", "modelo com espaço", "minha chave");
        await Page.GetByTestId("ai-form-save").ClickAsync();

        var form = Page.GetByTestId("ai-provider-form");
        await Assertions.Expect(form).ToContainTextAsync("https://");
        await Assertions.Expect(form).ToContainTextAsync("só pode ter letras");
        await Assertions.Expect(form).ToContainTextAsync("deve começar com AI_KEY_");
        Assert.Equal(before, await Page.GetByTestId("ai-provider-card").CountAsync());
        Assert.Equal(0, await Card(name).CountAsync());

        await Page.GetByTestId("ai-form-cancel").ClickAsync();
        await Assertions.Expect(form).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Form_PresetsFillTheAddressProtocolModelAndVariable()
    {
        await LoginAsAdminAsync();
        await OpenProvidersPageAsync();

        await Page.GetByTestId("ai-provider-new").ClickAsync();
        await Page.GetByTestId("ai-provider-form").WaitForAsync();
        await Page.GetByTestId("ai-form-preset").SelectOptionAsync("anthropic");

        await Assertions.Expect(Page.GetByTestId("ai-form-url")).ToHaveValueAsync("https://api.anthropic.com");
        await Assertions.Expect(Page.GetByTestId("ai-form-protocol")).ToHaveValueAsync("Anthropic");
        await Assertions.Expect(Page.GetByTestId("ai-form-env")).ToHaveValueAsync("ANTHROPIC");

        await Page.GetByTestId("ai-form-preset").SelectOptionAsync("openrouter");
        await Assertions.Expect(Page.GetByTestId("ai-form-url")).ToHaveValueAsync("https://openrouter.ai/api/v1");
        await Assertions.Expect(Page.GetByTestId("ai-form-protocol")).ToHaveValueAsync("OpenAiCompatible");
        await Assertions.Expect(Page.GetByTestId("ai-form-env")).ToHaveValueAsync("OPENROUTER");

        await Page.GetByTestId("ai-form-cancel").ClickAsync();
    }

    [Fact]
    public async Task Candidate_CannotOpenTheScreen_AndDoesNotSeeTheMenuLink()
    {
        Assert.True(await TryLoginAsync(CandidateEmail, TestPassword));
        await GotoAsync("/");
        await WaitForBlazorAsync();
        Assert.Equal(0, await Page.GetByTestId("nav-admin-ai-providers").CountAsync());

        await GotoAsync(PagePath);

        Assert.Contains("AccessDenied", Page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.GetByTestId("ai-provider-card").CountAsync());
        Assert.Equal(0, await Page.GetByTestId("ai-provider-new").CountAsync());
    }

    [Fact]
    public async Task Advertiser_CannotOpenTheScreen()
    {
        Assert.True(await TryLoginAsync(AdvertiserEmail, TestPassword));

        await GotoAsync(PagePath);

        Assert.Contains("AccessDenied", Page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.GetByTestId("ai-provider-new").CountAsync());
    }

    [Fact]
    public async Task Anonymous_IsSentToTheLogin()
    {
        await GotoAsync(PagePath);

        Assert.Contains("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.GetByTestId("ai-provider-card").CountAsync());
    }
}

/// <summary>Servidor local que imita o protocolo de chat completions da OpenAI e guarda o que recebeu.</summary>
internal sealed class FakeOpenAiServer : IAsyncDisposable
{
    private WebApplication _app = null!;
    private int _calls;

    public string BaseUrl { get; private set; } = string.Empty;

    public string? LastAuthorization { get; private set; }

    public int Calls => Volatile.Read(ref _calls);

    public static async Task<FakeOpenAiServer> StartAsync()
    {
        var server = new FakeOpenAiServer();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        server._app = builder.Build();
        server._app.MapPost("/v1/chat/completions", (HttpContext context) =>
        {
            Interlocked.Increment(ref server._calls);
            server.LastAuthorization = context.Request.Headers.Authorization;

            return Results.Json(new
            {
                model = "modelo-e2e",
                choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = "ok" } } },
                usage = new { prompt_tokens = 3, completion_tokens = 1 }
            });
        });

        await server._app.StartAsync();
        server.BaseUrl = server._app.Urls.First().TrimEnd('/');
        return server;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
