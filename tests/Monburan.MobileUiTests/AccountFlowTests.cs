namespace Monburan.MobileUiTests;

/// <summary>Exclusão de conta pelo app (exigência das lojas) e navegação geral.</summary>
public class AccountFlowTests
{
    private static AppSession OpenProfile(string email)
    {
        var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");
        app.GoTo("Perfil");
        app.WaitForText("h1.page-title", "Perfil");
        return app;
    }

    [MobileUiFact]
    public async Task DeleteAccount_WithTheRightPassword_RemovesTheAccount()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenProfile(email);

        app.Type("#delete-password", TestApi.Password);
        app.TapText("button", "Excluir minha conta");
        app.WaitForText(".sheet__title", "Excluir sua conta?");
        app.Capture("16-confirmacao-exclusao-de-conta");
        app.TapText(".sheet__actions button", "Excluir conta");

        // Conta apagada: o app sai da sessão e volta ao início sem login
        app.WaitForText("#home-title", "O trabalho certo muda tudo");
        // E a mesma senha já não entra mais
        app.LogIn(email, TestApi.Password);
        app.WaitForText(".alert-danger", "");
        app.WaitForText("h1.page-title", "Entrar");
    }

    [MobileUiFact]
    public async Task DeleteAccount_WithoutThePassword_AsksForIt()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenProfile(email);

        app.TapText("button", "Excluir minha conta");

        app.WaitForText(".field-error", "Informe sua senha para confirmar");
    }

    [MobileUiFact]
    public async Task DeleteAccount_WhenCancelled_KeepsTheAccount()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = OpenProfile(email);

        app.Type("#delete-password", TestApi.Password);
        app.TapText("button", "Excluir minha conta");
        app.WaitForText(".sheet__title", "Excluir sua conta?");
        app.TapText(".sheet__actions button", "Cancelar");

        app.WaitForText("h1.page-title", "Perfil");
        // A conta continua valendo: o app segue logado e o painel abre
        app.GoTo("Início");
        app.WaitForText("h1.page-title", "Continue sua jornada");
    }

    [MobileUiFact]
    public async Task BottomNavigation_OpensEachSection()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");

        app.GoTo("Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        app.GoTo("Candidaturas");
        app.WaitForText("h1.page-title", "Minhas candidaturas");
        app.GoTo("Avisos");
        app.WaitForText("h1.page-title", "Avisos");
        app.GoTo("Perfil");
        app.WaitForText("h1.page-title", "Perfil");
        app.GoTo("Início");
        app.WaitForText("h1.page-title", "Continue sua jornada");
    }

    [MobileUiFact]
    public void ThemeToggle_SwitchesBetweenDarkAndLight()
    {
        using var app = new AppSession();
        app.WaitForText("#home-title", "O trabalho certo muda tudo");
        var before = app.AttributeOf(".theme-toggle", "aria-pressed");

        app.Tap(".theme-toggle");

        app.WaitUntil("o botão de tema trocar de estado", () =>
            app.AttributeOf(".theme-toggle", "aria-pressed") != before);
        app.Capture("17-tema-alternado");
    }

    [MobileUiFact]
    public async Task Jobs_FilteredByRemote_ShowOnlyRemoteJobs()
    {
        var email = await TestApi.RegisterCandidateAsync();
        using var app = new AppSession();
        app.LogIn(email, TestApi.Password);
        app.WaitForText("h1.page-title", "Continue sua jornada");
        app.GoTo("Vagas");
        app.WaitForText("h1.page-title", "Oportunidades");
        app.WaitFor("article.job-card");

        app.TapText(".filter-chips .chip", "Remoto");

        app.WaitFor(".filter-chips .chip[aria-pressed=true]");
        // Ou há vagas e todas são remotas, ou a lista ficou vazia: lista em carregamento não conta
        app.WaitUntil("a lista conter só vagas remotas (ou a mensagem de lista vazia)", () =>
        {
            var cards = app.TextsOf("article.job-card");
            if (cards.Count > 0)
                return cards.All(card => card.Contains("Remoto", StringComparison.OrdinalIgnoreCase));
            return app.TextsOf("body").Any(text => text.Contains("Nenhuma vaga encontrada", StringComparison.OrdinalIgnoreCase));
        });
        app.Capture("18-filtro-remoto");
    }
}
