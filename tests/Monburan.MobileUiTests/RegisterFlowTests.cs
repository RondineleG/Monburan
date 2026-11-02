namespace Monburan.MobileUiTests;

/// <summary>Criar conta e validações dos formulários de entrada.</summary>
public class RegisterFlowTests
{
    private const string StrongPassword = "Cavalo-Azul-Bateria-7";

    private static void OpenRegister(AppSession app)
    {
        app.TapText("a.btn", "Criar conta gratuita");
        app.WaitForText("h1.page-title", "Criar conta");
    }

    private static void FillRegister(AppSession app, string email, string password, string confirm)
    {
        app.Type("#register-name", "Candidato Cadastro Tela");
        app.Type("#register-email", email);
        app.Type("#register-password", password);
        app.Type("#register-confirm", confirm);
    }

    [MobileUiFact]
    public void Register_WithValidData_CreatesTheAccount_AndOpensTheDashboard()
    {
        using var app = new AppSession();
        OpenRegister(app);

        FillRegister(app, $"ui.cadastro.{Guid.NewGuid():N}@teste.com", StrongPassword, StrongPassword);
        app.Tap("form button[type=submit]");

        app.WaitForText("h1.page-title", "Continue sua jornada");
        app.WaitFor(".progress-card");
        app.Capture("11-conta-criada-painel");
    }

    [MobileUiFact]
    public void Register_WithDifferentPasswords_ShowsTheMismatchError()
    {
        using var app = new AppSession();
        OpenRegister(app);

        FillRegister(app, $"ui.cadastro.{Guid.NewGuid():N}@teste.com", StrongPassword, StrongPassword + "x");
        app.Tap("form button[type=submit]");

        app.WaitForText(".field-error", "As senhas não conferem");
        app.WaitForText("h1.page-title", "Criar conta");
    }

    [MobileUiFact]
    public void Register_WithAnEmptyForm_ShowsTheRequiredFieldErrors()
    {
        using var app = new AppSession();
        OpenRegister(app);

        app.Tap("form button[type=submit]");

        app.WaitForText(".field-error", "Informe seu nome");
        app.WaitForText(".field-error", "Informe um e-mail válido");
        app.Capture("12-cadastro-campos-obrigatorios");
    }

    [MobileUiFact]
    public void Register_WithAShortPassword_ShowsThePolicyError()
    {
        using var app = new AppSession();
        OpenRegister(app);

        FillRegister(app, $"ui.cadastro.{Guid.NewGuid():N}@teste.com", "curta1", "curta1");
        app.Tap("form button[type=submit]");

        app.WaitForText(".field-error", "pelo menos 10 caracteres");
    }

    [MobileUiFact]
    public void Login_WithEmptyFields_AsksForEmailAndPassword()
    {
        using var app = new AppSession();
        app.TapText("a.btn", "Entrar");
        app.WaitForText("h1.page-title", "Entrar");

        app.Tap("form button[type=submit]");

        app.WaitForText(".alert-danger", "Informe e-mail e senha");
    }
}
