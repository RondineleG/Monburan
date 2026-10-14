using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class ShellInteractionTests : E2ETestBase
{
    public ShellInteractionTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Anonymous_Plans_ShouldShowPricesWithoutLogin()
    {
        var response = await GotoAsync("/plans");
        Assert.True(response!.Ok, $"Plans retornou {response.Status}");
        await WaitForBlazorAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Premium", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Usuário não identificado", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Erro interno", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(await Page.Locator("a[href*='Account/Register']").CountAsync() > 0,
            "Anônimo deveria ver CTA para criar conta de empresa");
    }

    [Fact]
    public async Task Theme_Toggle_ShouldSwitchAndPersist()
    {
        await GotoAsync("/");
        await WaitForBlazorAsync();

        var initial = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", initial);

        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        var switched = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        Assert.Equal("light", switched);

        await Page.ReloadAsync();
        await WaitForBlazorAsync();
        var persisted = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        Assert.Equal("light", persisted);

        // volta ao dark para não vazar estado
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
    }

    [Fact]
    public async Task Theme_ShouldPersist_AcrossInAppNavigation()
    {
        await GotoAsync("/");
        await WaitForBlazorAsync();

        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        Assert.Equal("light", await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')"));

        // Navega por links internos (enhanced navigation / full reload) — o tema deve manter.
        foreach (var path in new[] { "/jobs", "/plans", "/" })
        {
            await Page.Locator($"a[href='{path}']").First.ClickAsync();
            await WaitForBlazorAsync();
            var theme = await Page.EvaluateAsync<string?>(
                "() => document.documentElement.getAttribute('data-theme')");
            Assert.True(theme == "light", $"Tema resetou para '{theme}' ao navegar para {path}");
        }

        // restaura dark
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
    }

    [Fact]
    public async Task Sidebar_Pin_ShouldKeepExpanded()
    {
        await Page.SetViewportSizeAsync(1280, 800);
        await GotoAsync("/");
        await WaitForBlazorAsync();

        var railBefore = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')");
        Assert.True(railBefore, "Sidebar deveria iniciar recolhida");

        // O pin só aparece quando a rail expande no hover
        await Page.Locator("[data-testid='app-sidebar']").HoverAsync();
        await Page.Locator("[data-action='sidebar-pin']").ClickAsync(new LocatorClickOptions { Force = true });

        var railAfter = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')");
        Assert.False(railAfter, "Pin deveria expandir a sidebar");

        await Page.ReloadAsync();
        await WaitForBlazorAsync();
        var persisted = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')");
        Assert.False(persisted, "Expansão deveria persistir via localStorage");

        // restaura
        await Page.EvaluateAsync(
            "() => { document.documentElement.classList.add('sidebar-rail'); localStorage.setItem('monburan-sidebar','rail'); }");
    }

    [Fact]
    public async Task Logout_Button_ShouldSignOut()
    {
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await GotoAsync("/dashboard");
        await WaitForBlazorAsync();

        var logout = Page.GetByTestId("nav-logout");
        await logout.WaitForAsync();
        await logout.ClickAsync();

        await Page.WaitForURLAsync(url => !url.Contains("/dashboard"));
        await WaitForBlazorAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("SUA JORNADA", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Candidate_Plans_ShouldShowReadOnlyPrices()
    {
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await GotoAsync("/plans");
        await WaitForBlazorAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Premium", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empresa", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.Locator("button:has-text('Fazer upgrade')").CountAsync());
    }
}
