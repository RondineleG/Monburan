using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5095";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Garante que a aplicação está no ar antes de rodar qualquer teste.
        var api = await Playwright.APIRequest.NewContextAsync();
        var response = await api.GetAsync($"{BaseUrl}/health");
        if (!response.Ok)
            throw new InvalidOperationException(
                $"Aplicação não está disponível em {BaseUrl}. Suba com: " +
                "ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5095");
        await api.DisposeAsync();

        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null)
            await Browser.DisposeAsync();
        Playwright?.Dispose();
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<PlaywrightFixture>
{
}

[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected PlaywrightFixture Fixture { get; }

    protected IBrowserContext Context = null!;
    protected IPage Page = null!;

    // O navegador relata no console tudo o que o Content-Security-Policy bloqueou; qualquer teste que provoque isso falha
    protected readonly List<string> CspViolations = [];

    /// <summary>
    /// O Playwright avalia os predicados de <c>WaitForFunctionAsync</c> com eval na página, e o CSP do site (sem unsafe-eval)
    /// bloqueia isso. Por isso a suíte roda com o CSP ignorado; só <c>CspEnforcementTests</c> o aplica de verdade.
    /// </summary>
    protected virtual bool BypassCsp => true;
    protected static string BaseUrl => PlaywrightFixture.BaseUrl;

    protected const string CandidateEmail = "joao.silva@email.com";
    protected const string AdvertiserEmail = "contato@techsolutions.com";
    protected const string TestPassword = "Monburan123";

    protected E2ETestBase(PlaywrightFixture fixture) => Fixture = fixture;

    public virtual async Task InitializeAsync()
    {
        Context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BypassCSP = BypassCsp
        });
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(20000);
        Page.Console += (_, message) =>
        {
            if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
                CspViolations.Add(message.Text);
        };
    }

    public async Task DisposeAsync()
    {
        if (Context != null)
            await Context.CloseAsync();

        Assert.True(CspViolations.Count == 0,
            "O Content-Security-Policy bloqueou algo nesta página:\n" + string.Join("\n", CspViolations.Distinct()));
    }

    protected async Task<IResponse?> GotoAsync(string path) =>
        await Page.GotoAsync(BaseUrl + path, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

    // Páginas interativas precisam do circuito Blazor estabelecido antes de cliques.
    protected async Task WaitForBlazorAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        if (await Page.GetByTestId("app-shell").CountAsync() > 0)
        {
            await Page.WaitForFunctionAsync("() => document.querySelector('[data-testid=\"app-shell\"]')?.getAttribute('data-blazor-ready') === 'true'")
                ;
            return;
        }

        await Page.GetByTestId("auth-content").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });
    }

    protected async Task<bool> TryLoginAsync(string email, string password)
    {
        await GotoAsync("/entrar");
        await Page.GetByTestId("login-email").WaitForAsync();
        await Page.GetByTestId("login-email").FillAsync(email);
        await Page.GetByTestId("login-password").FillAsync(password);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Page.WaitForFunctionAsync("""
            () => !!document.querySelector('[data-testid="nav-jobs"]') ||
                  !!document.querySelector('[data-testid="account-status"]')
            """);

        return !Page.Url.Contains("/entrar", StringComparison.OrdinalIgnoreCase);
    }

    protected async Task<bool> TryRegisterAsync(
        string email,
        string password,
        string? confirmationPassword = null,
        string accountType = "candidate")
    {
        await GotoAsync("/criar-conta");
        await Page.GetByTestId("register-email").WaitForAsync();
        await Page.GetByTestId("register-name").FillAsync("Pessoa Teste E2E");
        await Page.GetByTestId(accountType == "advertiser" ? "register-role-advertiser" : "register-role-candidate")
            .CheckAsync();
        await Page.GetByTestId("register-email").FillAsync(email);
        await Page.GetByTestId("register-password").FillAsync(password);
        await Page.GetByTestId("register-confirm-password")
            .FillAsync(confirmationPassword ?? password);
        await Page.GetByTestId("register-submit").ClickAsync();
        await Page.WaitForFunctionAsync("""
            () => !!document.querySelector('[data-testid="nav-jobs"]') ||
                  !!document.querySelector('[data-testid="account-status"]') ||
                  !!document.querySelector('[data-testid="register-confirm-password-error"]')?.textContent?.trim()
            """);

        return !Page.Url.Contains("/criar-conta", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsAuthenticatedAsync()
    {
        if (!Page.Url.Contains("/entrar", StringComparison.OrdinalIgnoreCase) &&
            !Page.Url.Contains("/criar-conta", StringComparison.OrdinalIgnoreCase))
            return true;

        var cookies = await Context.CookiesAsync();
        return cookies.Any(cookie =>
            cookie.Name.Contains("Identity.Application", StringComparison.OrdinalIgnoreCase) ||
            cookie.Name.Contains("AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase));
    }

    // Registro auto-loga o usuário (RequireConfirmedAccount = false).
    // Idempotente: tenta login primeiro; se falhar, registra.
    protected async Task LoginOrRegisterAsync(string email, string password)
    {
        if (await TryLoginAsync(email, password))
            return;

        var registered = await TryRegisterAsync(email, password);
        Assert.True(registered, $"Não foi possível autenticar nem registrar {email}");
    }

    protected async Task AssertLoggedInAsync()
    {
        await GotoAsync("/conta");
        await Page.GetByTestId("account-manage-page").WaitForAsync();
        Assert.DoesNotContain("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    protected async Task AssertLoggedOutAsync()
    {
        await GotoAsync("/conta");
        await Page.GetByTestId("login-email").WaitForAsync();
        Assert.Contains("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
