using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class ShellInteractionTests : E2ETestBase
{
    public ShellInteractionTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task AddressCreate_WarnsBeforeLeavingWithUnsavedChanges()
    {
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await GotoAsync("/address/create");
        await WaitForBlazorAsync();

        await Page.Locator("#street").FillAsync("Rua alterada para testar o aviso");
        await Page.Locator("#street").BlurAsync();
        Assert.Equal("Rua alterada para testar o aviso", await Page.Locator("#street").InputValueAsync());
        await Page.GetByTestId("page-back").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("confirm-dialog")).ToBeVisibleAsync();
        await Page.GetByTestId("confirm-cancel").ClickAsync();
        Assert.EndsWith("/address/create", Page.Url);

        Assert.Equal("Rua alterada para testar o aviso", await Page.Locator("#street").InputValueAsync());

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancelar" }).ClickAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await Page.WaitForURLAsync(url => url.EndsWith("/address/list", StringComparison.OrdinalIgnoreCase));
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

        // Sem escolha salva o tema segue o sistema; o botão troca e a escolha persiste
        var initial = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        var expected = initial == "dark" ? "light" : "dark";

        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        var switched = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        Assert.Equal(expected, switched);

        await Page.ReloadAsync();
        await WaitForBlazorAsync();
        var persisted = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        Assert.Equal(expected, persisted);

        // volta ao tema inicial para não vazar estado
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
    }

    [Fact]
    public async Task Theme_ShouldPersist_AcrossInAppNavigation()
    {
        await GotoAsync("/");
        await WaitForBlazorAsync();

        var initial = await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        var chosen = initial == "dark" ? "light" : "dark";
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
        Assert.Equal(chosen, await Page.EvaluateAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')"));

        // Navega por links internos (enhanced navigation / full reload) — o tema deve manter.
        foreach (var path in new[] { "/jobs", "/plans", "/" })
        {
            await Page.Locator($"a[href='{path}']").First.ClickAsync();
            await WaitForBlazorAsync();
            var theme = await Page.EvaluateAsync<string?>(
                "() => document.documentElement.getAttribute('data-theme')");
            Assert.True(theme == chosen, $"Tema resetou para '{theme}' ao navegar para {path}");
        }

        // restaura o tema inicial
        await Page.Locator("[data-action='theme-toggle']").ClickAsync();
    }

    [Fact]
    public async Task Sidebar_Pin_ShouldKeepExpanded()
    {
        // O menu lateral é da área logada: começa expandido, o botão recolhe e a escolha persiste
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await Page.SetViewportSizeAsync(1280, 800);
        await GotoAsync("/");
        await WaitForBlazorAsync();

        var railBefore = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')");
        Assert.False(railBefore, "Menu lateral deveria iniciar expandido");

        await Page.Locator("[data-action='sidebar-pin']").ClickAsync();
        Assert.True(await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')"), "O botão deveria recolher o menu");
        Assert.Equal("false", await Page.Locator("[data-action='sidebar-pin']").GetAttributeAsync("aria-expanded"));

        await Page.ReloadAsync();
        await WaitForBlazorAsync();
        Assert.True(await Page.EvaluateAsync<bool>(
            "() => document.documentElement.classList.contains('sidebar-rail')"), "A escolha deveria persistir");

        // restaura o menu expandido
        await Page.Locator("[data-action='sidebar-pin']").ClickAsync();
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
