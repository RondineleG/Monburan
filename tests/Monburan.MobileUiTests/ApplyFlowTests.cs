namespace Monburan.MobileUiTests;

/// <summary>Candidatar-se a uma vaga, acompanhar a candidatura e salvar vagas.</summary>
public class ApplyFlowTests
{
    private static AppSession StartLoggedIn(string email)
    {
        var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");
        return app;
    }

    [MobileUiFact]
    public async Task Apply_ToAnOpenJob_ConfirmsTheApplication_AndListsItUnderMyApplications()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = StartLoggedIn(email);
        app.GoTo("Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        var jobTitle = app.WaitFor("article.job-card .card-title").Text;
        app.Tap("article.job-card a.job-card__link");
        app.WaitForText("h1", jobTitle);

        app.TapText("button", "Candidatar-se");
        app.WaitForText("h1.page-title", "Candidatar-se");
        app.Type("#apply-notes", "Candidatura enviada pelo teste de tela.");
        app.TapText("form button[type=submit]", "Confirmar candidatura");

        app.WaitForText("[role=status]", "Candidatura enviada!");
        app.Capture("13-candidatura-enviada");

        app.TapText("a.btn", "Ver minhas candidaturas");
        app.WaitForText("h1.page-title", "Minhas candidaturas");
        app.WaitForText("article.card .card-title", jobTitle);
    }

    [MobileUiFact]
    public async Task SaveJob_FromTheList_ShowsUnderSavedJobs()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = StartLoggedIn(email);
        app.GoTo("Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        var jobTitle = app.WaitFor("article.job-card .card-title").Text;

        app.Tap("article.job-card button.save-toggle");
        app.WaitFor("article.job-card button.save-toggle[aria-pressed=true]");

        app.GoTo("Candidaturas");
        app.WaitForText("h1.page-title", "Minhas candidaturas");
        app.TapText("a.tab", "Vagas salvas");
        app.WaitForText("article.card .card-title", jobTitle);
        app.Capture("14-vagas-salvas");
    }

    [MobileUiFact]
    public async Task Applications_WithoutAnyApplication_ShowsTheEmptyList()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = StartLoggedIn(email);

        app.GoTo("Candidaturas");

        app.WaitForText("h1.page-title", "Minhas candidaturas");
        app.WaitForText("a.tab", "Candidaturas");
        app.WaitForText("a.tab", "Vagas salvas");
        app.WaitForText("a.tab", "Alertas");
    }
}
