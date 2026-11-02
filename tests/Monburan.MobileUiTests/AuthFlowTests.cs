namespace Monburan.MobileUiTests;

/// <summary>Telas de entrada do app: boas-vindas, login, sessão guardada e saída da conta.</summary>
public class AuthFlowTests
{
    [MobileUiFact]
    public void FirstLaunch_WithoutSession_ShowsTheWelcomeScreen()
    {
        using var app = new AppSession();

        app.WaitForText("#home-title", "O trabalho certo muda tudo");
        app.WaitForText("a.btn", "Entrar");
        app.WaitForText("a.btn", "Criar conta gratuita");
        app.Capture("01-boas-vindas");
    }

    [MobileUiFact]
    public async Task Login_WithAWrongPassword_ShowsTheError_AndStaysOnLogin()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();

        app.LogIn(email, "SenhaErrada123");

        app.WaitForText(".alert-danger", "incorretos");
        app.WaitForText("h1.page-title", "Entrar");
        app.Capture("02-login-senha-errada");
    }

    [MobileUiFact]
    public async Task Login_WithValidCredentials_OpensTheDashboard_WithTheBottomNavigation()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();

        app.LogIn(email, TestApi.Password);

        app.WaitForText("h1.page-title", "Continue sua jornada");
        foreach (var label in new[] { "Início", "Vagas", "Candidaturas", "Avisos", "Perfil" })
            app.WaitForText(".bottom-nav__label", label);
        app.Capture("03-painel-apos-login");
    }

    [MobileUiFact]
    public async Task Session_SurvivesClosingAndReopeningTheApp()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.Relaunch();

        app.WaitForText("h1.page-title", "Continue sua jornada");
        // O título aparece antes dos dados: espera o painel carregar para a evidência não mostrar o spinner
        app.WaitFor(".progress-card");
        app.Capture("04-sessao-mantida-ao-reabrir");
    }

    [MobileUiFact]
    public async Task Logout_GoesToTheLoginScreen_AndTheSessionIsGone()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.TapText(".bottom-nav__link", "Perfil");
        app.WaitForText("h1.page-title", "Perfil");
        app.Capture("05-perfil");
        app.TapText("button", "Sair da conta");

        // O app leva à tela de login (Profile.LogoutAsync). Reabrir o app mostra que a sessão foi apagada
        app.WaitForText("h1.page-title", "Entrar");
        app.Capture("06-login-apos-sair");
        app.Relaunch();
        app.WaitForText("#home-title", "O trabalho certo muda tudo");
        app.Capture("07-boas-vindas-apos-sair-e-reabrir");
    }
}
