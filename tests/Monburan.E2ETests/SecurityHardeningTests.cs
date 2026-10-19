using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>Bloqueio da conta por tentativas e política de senha, pela interface.</summary>
public class SecurityHardeningTests : E2ETestBase
{
    private const string GoodPassword = "Cavalo-Azul-Bateria-7";

    public SecurityHardeningTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private async Task SubmitLoginAsync(IPage page, string email, string password)
    {
        await page.GotoAsync(BaseUrl + "/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.GetByTestId("login-email").WaitForAsync();
        await page.GetByTestId("login-email").FillAsync(email);
        await page.GetByTestId("login-password").FillAsync(password);
        await page.GetByTestId("login-submit").ClickAsync();
        await page.WaitForFunctionAsync("""
            () => location.pathname.toLowerCase() !== '/account/login' ||
                  !!document.querySelector('.alert, [role="alert"], .text-danger')?.textContent?.trim()
            """);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksTheAccount_EvenForTheRightPassword()
    {
        var email = $"bloqueio-{Guid.NewGuid():N}@test.com";
        Assert.True(await TryRegisterAsync(email, GoodPassword), "Não foi possível criar a conta de teste");

        // Contexto novo: sem a sessão aberta pelo cadastro
        await using var other = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await other.NewPageAsync();
        page.SetDefaultTimeout(20000);

        // As quatro primeiras só recusam; a quinta senha errada já bloqueia a conta
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await SubmitLoginAsync(page, email, "senha-errada-" + attempt);
            Assert.Contains("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
        }

        await SubmitLoginAsync(page, email, "senha-errada-5");
        Assert.Contains("/Account/Lockout", page.Url, StringComparison.OrdinalIgnoreCase);

        // Com a conta bloqueada nem a senha certa entra
        await SubmitLoginAsync(page, email, GoodPassword);

        Assert.Contains("/Account/Lockout", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporariamente bloqueado", await page.Locator("body").InnerTextAsync());
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsTheSameMessageForAnUnknownAccount()
    {
        await using var other = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await other.NewPageAsync();
        page.SetDefaultTimeout(20000);

        await SubmitLoginAsync(page, $"nao-existe-{Guid.NewGuid():N}@test.com", GoodPassword);

        Assert.Contains("e-mail ou senha inválidos", await page.Locator("body").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_NineCharacterPassword_IsRefusedWithTheNewMinimum()
    {
        await GotoAsync("/Account/Register");
        await Page.GetByTestId("register-email").WaitForAsync();
        await Page.GetByTestId("register-name").FillAsync("Pessoa Teste");
        await Page.GetByTestId("register-role-candidate").CheckAsync();
        await Page.GetByTestId("register-email").FillAsync($"curta-{Guid.NewGuid():N}@test.com");
        await Page.GetByTestId("register-password").FillAsync("Abc12345x");
        await Page.GetByTestId("register-confirm-password").FillAsync("Abc12345x");
        await Page.GetByTestId("register-submit").ClickAsync();

        await Page.WaitForFunctionAsync("() => document.body.innerText.includes('pelo menos 10 caracteres')");
        Assert.Contains("/Account/Register", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_CommonPassword_IsRefusedByTheServer()
    {
        await GotoAsync("/Account/Register");
        await Page.GetByTestId("register-email").WaitForAsync();
        await Page.GetByTestId("register-name").FillAsync("Pessoa Teste");
        await Page.GetByTestId("register-role-candidate").CheckAsync();
        await Page.GetByTestId("register-email").FillAsync($"comum-{Guid.NewGuid():N}@test.com");
        await Page.GetByTestId("register-password").FillAsync("Senha12345");
        await Page.GetByTestId("register-confirm-password").FillAsync("Senha12345");
        await Page.GetByTestId("register-submit").ClickAsync();

        await Page.WaitForFunctionAsync("() => document.body.innerText.includes('muito comum')");
        Assert.Contains("/Account/Register", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
