namespace Monburan.MobileUiTests;

/// <summary>Currículos criados dentro do app.</summary>
public class ResumeFlowTests
{
    private static AppSession OpenNewResumeForm(string email)
    {
        var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");
        app.GoTo("Perfil");
        app.WaitForText("h1.page-title", "Perfil");
        app.TapText("a", "Meus currículos");
        app.WaitForText("h1.page-title", "Meus currículos");
        app.TapText("a.btn", "Criar currículo");
        app.WaitForText("h1.page-title", "Novo currículo");
        return app;
    }

    [MobileUiFact]
    public async Task Resume_Create_AppearsInTheList()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenNewResumeForm(email);
        var title = $"Desenvolvedor Teste {DateTime.UtcNow:HHmmss}";

        app.Type("#resume-title", title);
        app.Type("#resume-summary", "Currículo criado pelo teste de tela do aplicativo.");
        app.TapText("form button[type=submit]", "Criar currículo");

        // Depois de criar, o app abre o detalhe do currículo novo (título dele, status e as seções vazias)
        app.WaitForText("body", title);
        app.WaitForText("body", "Nenhuma experiência adicionada");
        app.Capture("15-curriculo-criado");
        app.GoTo("Perfil");
        app.TapText("a", "Meus currículos");
        app.WaitForText(".card-title", title);
    }

    [MobileUiFact]
    public async Task Resume_WithoutATitle_ShowsTheValidationMessage()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenNewResumeForm(email);

        app.TapUntilShown("form button[type=submit]", "Criar currículo", ".field-error", "Informe um título para o currículo");
        app.WaitForText("h1.page-title", "Novo currículo");
    }

    [MobileUiFact]
    public async Task Resume_WithATooShortTitle_ShowsTheValidationMessage()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenNewResumeForm(email);

        app.Type("#resume-title", "ab");
        app.TapUntilShown("form button[type=submit]", "Criar currículo", ".field-error", "pelo menos 3 caracteres");
    }
}
