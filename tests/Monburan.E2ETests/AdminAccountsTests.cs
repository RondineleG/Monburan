using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>Gestão de contas pela interface: inativar, reativar e as travas, com o efeito visto de outra sessão.</summary>
public class AdminAccountsTests : E2ETestBase
{
    private const string AdminEmail = "admin@monburan.com.br";
    private const string AccountsPath = "/admin/accounts";
    private const string GoodPassword = "Cavalo-Azul-Bateria-7";

    public AdminAccountsTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private ILocator Row(string email) => Page.Locator($"[data-testid='acct-row'][data-email='{email}']");

    private async Task OpenAccountsAsync()
    {
        await GotoAsync(AccountsPath);
        await WaitForBlazorAsync();
        await Page.GetByTestId("acct-search").WaitForAsync();
    }

    private async Task SearchAsync(string term)
    {
        await Page.GetByTestId("acct-search").FillAsync(term);
        await Page.GetByTestId("acct-apply").ClickAsync();
    }

    private async Task ManageAsync(string email)
    {
        await SearchAsync(email);
        await Row(email).GetByTestId("acct-manage").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("acct-panel")).ToHaveAttributeAsync("data-email", email);
    }

    /// <summary>Cria uma conta de candidato numa sessão separada (a do administrador não é tocada).</summary>
    private async Task<string> RegisterCandidateInOtherContextAsync()
    {
        var email = $"conta-{Guid.NewGuid():N}@test.com";
        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20000);
        await page.GotoAsync(BaseUrl + "/Account/Register");
        await page.GetByTestId("register-email").WaitForAsync();
        await page.GetByTestId("register-name").FillAsync("Pessoa Gerenciada");
        await page.GetByTestId("register-role-candidate").CheckAsync();
        await page.GetByTestId("register-email").FillAsync(email);
        await page.GetByTestId("register-password").FillAsync(GoodPassword);
        await page.GetByTestId("register-confirm-password").FillAsync(GoodPassword);
        await page.GetByTestId("register-submit").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Account/Register", StringComparison.OrdinalIgnoreCase));
        return email;
    }

    private async Task<bool> CanLogInInFreshContextAsync(string email, string password)
    {
        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20000);
        await page.GotoAsync(BaseUrl + "/Account/Login");
        await page.GetByTestId("login-email").FillAsync(email);
        await page.GetByTestId("login-password").FillAsync(password);
        await page.GetByTestId("login-submit").ClickAsync();
        await page.WaitForFunctionAsync("""
            () => location.pathname.toLowerCase() !== '/account/login' ||
                  !!document.querySelector('.alert, [role="alert"]')?.textContent?.trim()
            """);
        return !page.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_DeactivatesAndReactivatesAnAccount_WithTheEffectSeenAtLogin()
    {
        var email = await RegisterCandidateInOtherContextAsync();
        Assert.True(await CanLogInInFreshContextAsync(email, GoodPassword), "A conta recém-criada deveria entrar");
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenAccountsAsync();

        // Inativar: o motivo é obrigatório e a conta fica fora do ar
        await ManageAsync(email);
        await Page.GetByTestId("acct-deactivate").ClickAsync();
        await Page.GetByTestId("acct-deactivate-confirm").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("acct-reason-error")).ToContainTextAsync("motivo");

        await Page.GetByTestId("acct-reason").FillAsync("Perfil falso denunciado por três empresas");
        await Page.GetByTestId("acct-deactivate-confirm").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("acct-panel-state")).ToHaveTextAsync("Inativa");
        await Assertions.Expect(Page.GetByTestId("acct-panel-deactivation")).ToContainTextAsync("Perfil falso denunciado por três empresas");
        await Assertions.Expect(Page.GetByTestId("acct-panel-deactivation")).ToContainTextAsync(AdminEmail);

        // A pessoa não entra mais, e a mensagem diz o porquê (só para quem acertou a senha)
        Assert.False(await CanLogInInFreshContextAsync(email, GoodPassword));
        await using (var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true }))
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(BaseUrl + "/Account/Login");
            await page.GetByTestId("login-email").FillAsync(email);
            await page.GetByTestId("login-password").FillAsync(GoodPassword);
            await page.GetByTestId("login-submit").ClickAsync();
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync("sua conta está inativa");
        }

        // Reativar
        await Page.GetByTestId("acct-activate").ClickAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("acct-panel-state")).ToHaveTextAsync("Ativa");
        Assert.True(await CanLogInInFreshContextAsync(email, GoodPassword), "Reativada, a conta volta a entrar");

        // Tudo ficou na auditoria
        await GotoAsync("/admin/audit");
        await WaitForBlazorAsync();
        await Page.GetByTestId("audit-search").FillAsync(email);
        await Page.GetByTestId("audit-apply").ClickAsync();
        var rows = Page.GetByTestId("audit-row");
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Conta inativada" })).ToHaveCountAsync(1);
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Conta reativada" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Admin_CannotDeactivateTheOwnAccount()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenAccountsAsync();

        await ManageAsync(AdminEmail);

        await Assertions.Expect(Page.GetByTestId("acct-panel-self")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("acct-deactivate")).ToBeDisabledAsync();
        await Assertions.Expect(Page.GetByTestId("acct-revoke-admin")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task FiltersAndCounters_ShowTheSituationOfTheAccounts()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenAccountsAsync();

        Assert.True(int.Parse(await Page.GetByTestId("acct-count-total").InnerTextAsync()) >= 5);
        Assert.True(int.Parse(await Page.GetByTestId("acct-count-active").InnerTextAsync()) >= 5);

        await Page.GetByTestId("acct-type").SelectOptionAsync("admin");
        await Page.GetByTestId("acct-apply").ClickAsync();
        await Assertions.Expect(Row(AdminEmail)).ToBeVisibleAsync();
        await Assertions.Expect(Row(CandidateEmail)).ToHaveCountAsync(0);

        await Page.GetByTestId("acct-type").SelectOptionAsync("");
        await SearchAsync("termo-que-nao-existe-" + Guid.NewGuid().ToString("N"));
        await Assertions.Expect(Page.GetByTestId("acct-empty")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_SeesTheMenuLink_AndOthersCannotOpenThePage()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await GotoAsync("/");
        await WaitForBlazorAsync();
        await Assertions.Expect(Page.GetByTestId("nav-admin-accounts")).ToBeVisibleAsync();

        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var candidate = await context.NewPageAsync();
        candidate.SetDefaultTimeout(20000);
        await candidate.GotoAsync(BaseUrl + "/Account/Login");
        await candidate.GetByTestId("login-email").FillAsync(CandidateEmail);
        await candidate.GetByTestId("login-password").FillAsync(TestPassword);
        await candidate.GetByTestId("login-submit").ClickAsync();
        await candidate.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));

        await candidate.GotoAsync(BaseUrl + AccountsPath);

        Assert.Contains("AccessDenied", candidate.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await candidate.GetByTestId("acct-row").CountAsync());
    }

    [Fact]
    public async Task Anonymous_IsSentToTheLogin()
    {
        await GotoAsync(AccountsPath);

        Assert.Contains("/Account/Login", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
