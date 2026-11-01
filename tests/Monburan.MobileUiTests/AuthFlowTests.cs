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
    }

    [MobileUiFact]
    public async Task Login_WithAWrongPassword_ShowsTheError_AndStaysOnLogin()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();

        app.LogIn(email, "SenhaErrada123");

        app.WaitForText(".alert-danger", "incorretos");
        app.WaitForText("h1.page-title", "Entrar");
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
    }

    [MobileUiFact]
    public async Task Logout_ReturnsToTheWelcomeScreen()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.TapText(".bottom-nav__link", "Perfil");
        app.WaitForText("h1.page-title", "Perfil");
        app.TapText("button", "Sair da conta");

        app.WaitForText("#home-title", "O trabalho certo muda tudo");
    }
}
