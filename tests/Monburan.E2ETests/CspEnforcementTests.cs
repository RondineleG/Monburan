using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>
/// O site rodando com o Content-Security-Policy de verdade (scripts só do próprio site, sem inline e sem eval): as páginas
/// principais de cada perfil têm de carregar, o Blazor conectar e os scripts do layout funcionar sem nenhuma violação.
/// Sem esperas por eval (<c>WaitForFunctionAsync</c>), que o próprio CSP bloquearia.
/// </summary>
public class CspEnforcementTests : E2ETestBase
{
    private const string AdminEmail = "admin@monburan.com.br";

    public CspEnforcementTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    protected override bool BypassCsp => false;

    private async Task LoginAsync(string email)
    {
        await Page.GotoAsync(BaseUrl + "/entrar");
        await Page.GetByTestId("login-email").FillAsync(email);
        await Page.GetByTestId("login-password").FillAsync(TestPassword);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Page.WaitForURLAsync(url => !url.Contains("/entrar", StringComparison.OrdinalIgnoreCase));
    }

    private async Task VisitAsync(string path)
    {
        await Page.GotoAsync(BaseUrl + path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var shell = Page.GetByTestId("app-shell");
        if (await shell.CountAsync() > 0)
        {
            // Quem põe este atributo é o app-shell.js, depois que o Blazor conecta: prova que o script externo roda sob o CSP
            await Assertions.Expect(shell).ToHaveAttributeAsync("data-blazor-ready", "true");
        }

        Assert.Empty(CspViolations);
    }

    [Fact]
    public async Task PublicPages_LoadWithoutAnyViolation()
    {
        foreach (var path in new[] { "/", "/jobs", "/entrar", "/criar-conta", "/privacidade", "/termos", "/plans" })
            await VisitAsync(path);
    }

    [Fact]
    public async Task AJobPage_LoadsWithoutAnyViolation()
    {
        await VisitAsync("/jobs");
        var firstJob = Page.Locator("a[href^='/jobs/']").First;
        await Assertions.Expect(firstJob).ToBeVisibleAsync();
        var href = await firstJob.GetAttributeAsync("href");

        await VisitAsync(href!);
    }

    [Fact]
    public async Task CandidateArea_LoadsWithoutAnyViolation()
    {
        await LoginAsync(CandidateEmail);

        foreach (var path in new[] { "/painel", "/curriculos", "/minhas-candidaturas", "/notifications", "/conta" })
            await VisitAsync(path);
    }

    [Fact]
    public async Task AdvertiserArea_LoadsWithoutAnyViolation()
    {
        await LoginAsync(AdvertiserEmail);

        foreach (var path in new[] { "/painel", "/minhas-vagas", "/candidaturas-recebidas", "/vagas/nova", "/plans", "/conta" })
            await VisitAsync(path);
    }

    [Fact]
    public async Task AdminArea_LoadsWithoutAnyViolation()
    {
        await LoginAsync(AdminEmail);

        foreach (var path in new[] { "/admin/candidatos", "/admin/empresas", "/admin/provedores-ia" })
            await VisitAsync(path);
    }

    [Fact]
    public async Task TheLayoutScripts_WorkUnderThePolicy_ThemeToggleAndMobileMenu()
    {
        await VisitAsync("/jobs");
        var html = Page.Locator("html");
        var before = await html.GetAttributeAsync("data-theme");

        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        await Assertions.Expect(html).Not.ToHaveAttributeAsync("data-theme", before ?? "dark");
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        await Assertions.Expect(html).ToHaveAttributeAsync("data-theme", before ?? "dark");

        // O menu lateral é da área logada
        await LoginAsync(CandidateEmail);
        await VisitAsync("/jobs");
        await Page.SetViewportSizeAsync(390, 844);
        await Page.GetByTestId("mobile-menu-toggle").ClickAsync();
        await Assertions.Expect(Page.Locator(".app-sidebar")).ToHaveClassAsync(new Regex("app-sidebar--open"));

        Assert.Empty(CspViolations);
    }

    [Fact]
    public async Task AnInjectedInlineScript_IsReallyBlocked_ProvingThePolicyIsEnforced()
    {
        await VisitAsync("/jobs");

        // Insere um <script> inline como um XSS faria: o navegador tem de recusar e relatar a violação
        try
        {
            await Page.AddScriptTagAsync(new PageAddScriptTagOptions { Content = "window.__xss = 1" });
        }
        catch (PlaywrightException)
        {
            // Esperado: o navegador recusa o script
        }

        for (var attempt = 0; attempt < 20 && CspViolations.Count == 0; attempt++)
            await Task.Delay(100);

        Assert.NotEmpty(CspViolations);
        Assert.Contains("script-src", CspViolations[0]);
        CspViolations.Clear(); // esperado neste teste
    }
}
