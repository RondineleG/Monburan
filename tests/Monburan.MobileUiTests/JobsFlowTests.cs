namespace Monburan.MobileUiTests;

/// <summary>Busca de vagas pelo app, contra a API com a massa de demonstração (seed do ambiente Development).</summary>
public class JobsFlowTests
{
    [MobileUiFact]
    public async Task Jobs_ListsTheOpenJobs_AndOpensTheDetails()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.TapText(".bottom-nav__link", "Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        var firstTitle = app.WaitFor("article.job-card .card-title").Text;
        Assert.False(string.IsNullOrWhiteSpace(firstTitle));

        app.Tap("article.job-card a.job-card__link");

        app.WaitForText("h1", firstTitle);
    }

    [MobileUiFact]
    public async Task Jobs_SearchWithNoMatch_ShowsTheEmptyState()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.TapText(".bottom-nav__link", "Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        app.Type("input[type=search]", "zzzz-nenhuma-vaga-com-este-nome");
        app.TapText("form[role=search] button", "Buscar");

        app.WaitForText("body", "Nenhuma vaga encontrada");
    }
}
