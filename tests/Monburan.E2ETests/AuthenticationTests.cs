using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class AuthenticationTests : E2ETestBase
{
    public AuthenticationTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Register_NewUser_ShouldAutoLogin()
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";

        var registered = await TryRegisterAsync(email, TestPassword);
        Assert.True(registered, "Registro não redirecionou para fora da página");

        await GotoAsync("/painel");
        await WaitForBlazorAsync();
        Assert.True(await Page.GetByTestId("nav-applications").IsVisibleAsync());
        await AssertLoggedInAsync();
    }

    [Fact]
    public async Task Register_Advertiser_ShouldOpenRecruiterWorkspace()
    {
        var email = $"recrutador-{Guid.NewGuid():N}@test.com";

        var registered = await TryRegisterAsync(email, TestPassword, accountType: "advertiser");
        Assert.True(registered, "Cadastro de recrutador não concluiu");

        await GotoAsync("/painel");
        await WaitForBlazorAsync();
        Assert.True(await Page.GetByTestId("nav-my-jobs").IsVisibleAsync());
        Assert.Contains("Ver candidaturas recebidas", await Page.Locator("body").InnerTextAsync());
    }

    [Fact]
    public async Task Login_EmptyFields_ShouldShowPortugueseValidation()
    {
        await GotoAsync("/entrar");
        await Page.GetByTestId("login-submit").ClickAsync();

        // A validação é renderizada pelo circuito Blazor depois do clique: sem esperar a
        // mensagem aparecer, a leitura do corpo pegava a página ainda sem a mensagem.
        await Page.GetByText("Informe seu e-mail.").WaitForAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Informe seu e-mail.", body);
        Assert.Contains("Informe sua senha.", body);
        Assert.DoesNotContain("The E-mail field is required.", body);
        Assert.DoesNotContain("The Senha field is required.", body);
    }

    [Fact]
    public async Task Register_ShouldShowLoginLinkAndPortugueseValidation()
    {
        await GotoAsync("/criar-conta");
        await Page.GetByTestId("register-submit").ClickAsync();

        // A validação é renderizada pelo circuito Blazor depois do clique: sem esperar a
        // mensagem aparecer, a leitura do corpo pegava a página ainda sem a mensagem.
        await Page.GetByText("Informe seu e-mail.").WaitForAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Já tem uma conta?", body);
        Assert.Contains("Entrar", body);
        Assert.Contains("Informe seu e-mail.", body);
        Assert.Contains("Informe uma senha.", body);
        Assert.DoesNotContain("The E-mail field is required.", body);
        Assert.DoesNotContain("The Senha field is required.", body);
    }

    [Fact]
    public async Task Register_WithMismatchedPassword_ShouldShowError()
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        var registered = await TryRegisterAsync(email, TestPassword, "OutraSenha9")
            ;

        Assert.False(registered);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldFail()
    {
        var loggedIn = await TryLoginAsync("naoexiste@test.com", "SenhaErrada1");
        Assert.False(loggedIn);
        await AssertLoggedOutAsync();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldAuthenticate()
    {
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await AssertLoggedInAsync();
    }

    [Fact]
    public async Task Login_Form_ShouldAuthenticateThroughBrowser()
    {
        await GotoAsync("/entrar");
        await Page.GetByTestId("login-email").WaitForAsync();
        await Page.GetByTestId("login-email").FillAsync(CandidateEmail);
        await Page.GetByTestId("login-password").FillAsync(TestPassword);
        await Page.GetByTestId("login-submit").ClickAsync();
        await Page.GetByTestId("nav-jobs").WaitForAsync();

        Assert.DoesNotContain("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
        await AssertLoggedInAsync();
    }

    [Fact]
    public async Task Anonymous_ShouldRedirect_ToLogin_OnProfilePages()
    {
        await GotoAsync("/perfil/candidato");
        await Page.GetByTestId("login-email").WaitForAsync();
        Assert.Contains("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
