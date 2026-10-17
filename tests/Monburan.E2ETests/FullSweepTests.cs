using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>
/// Bateria de varredura real: cria contas NOVAS de empresa e candidato,
/// navega por TODAS as telas e exercita os fluxos funcionais de ponta a ponta.
/// Falhas indicam funcionalidade quebrada — não apenas status HTTP.
/// </summary>
public class FullSweepTests : E2ETestBase
{
    public FullSweepTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private static readonly string[] AnonymousRoutes =
    [
        "/", "/jobs", "/plans", "/privacy", "/terms",
        "/Account/Login", "/Account/Register",
        "/Account/ForgotPassword", "/Account/ResendEmailConfirmation",
        "/404"
    ];

    private static readonly string[] CandidateRoutes =
    [
        "/dashboard", "/profile/candidate", "/jobs", "/my-applications",
        "/resumes", "/resumes/new", "/resumes/upload", "/notifications",
        "/my-subscription", "/address/list", "/address/create",
        "/plans", "/privacy", "/terms",
        "/Account/Manage", "/Account/Manage/ChangePassword",
        "/Account/Manage/Email", "/Account/Manage/PersonalData",
        "/Account/Manage/TwoFactorAuthentication"
    ];

    private static readonly string[] AdvertiserRoutes =
    [
        "/dashboard", "/profile/advertiser", "/jobs", "/jobs/create",
        "/my-jobs", "/received-applications", "/advertiser/upload-logo",
        "/notifications", "/my-subscription", "/plans",
        "/address/list", "/address/create",
        "/Account/Manage", "/Account/Manage/ChangePassword",
        "/Account/Manage/Email", "/Account/Manage/PersonalData"
    ];

    // ------------------------------------------------------------------
    // Varredura de rotas
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sweep_Anonymous_AllPublicRoutes()
    {
        var errors = await SweepRoutesAsync(AnonymousRoutes);
        AssertNoErrors(errors);
    }

    [Fact]
    public async Task Sweep_Candidate_AllRoutes()
    {
        var email = $"sweep-cand-{Guid.NewGuid():N}@test.com";
        await LoginOrRegisterAsync(email, TestPassword);

        var errors = await SweepRoutesAsync(CandidateRoutes);
        AssertNoErrors(errors);
    }

    [Fact]
    public async Task Sweep_Advertiser_AllRoutes()
    {
        var email = $"sweep-empresa-{Guid.NewGuid():N}@test.com";
        await RegisterAdvertiserAsync(email);

        var errors = await SweepRoutesAsync(AdvertiserRoutes);
        AssertNoErrors(errors);
    }

    // Assert.Empty trunca o texto dos itens e escondia a causa das falhas intermitentes
    private static void AssertNoErrors(List<string> errors) =>
        Assert.True(errors.Count == 0,
            $"{errors.Count} erro(s) na varredura:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

    private async Task<List<string>> SweepRoutesAsync(IEnumerable<string> routes)
    {
        var errors = new List<string>();
        var failedUrls = new List<string>();
        var currentRoute = "(antes da primeira rota)";
        Page.Response += (_, res) =>
        {
            if (res.Status >= 500)
                failedUrls.Add($"{res.Status} {res.Url}");
        };
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && !msg.Text.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            {
                // Erros de rede transitórios (resource load) ganham contexto da URL que falhou.
                var context = failedUrls.Count > 0 ? $" [{string.Join("; ", failedUrls)}]" : string.Empty;
                errors.Add($"JS console error durante {currentRoute} (página {Page.Url}, origem {msg.Location}): {msg.Text}{context}");
            }
        };

        // A página de chegada (depois do login ou do registro) pode ainda estar negociando o circuito Blazor. Navegar
        // antes disso aborta a negociação, e o navegador registra "Failed to fetch" como erro de console sem haver
        // defeito no app.
        if (Page.Url != "about:blank")
            await WaitForBlazorAsync();

        foreach (var route in routes)
        {
            currentRoute = route;
            var response = await GotoAsync(route);
            if (response is { Status: >= 500 })
            {
                errors.Add($"{route}: HTTP {response.Status}");
                continue;
            }

            await WaitForBlazorAsync();

            var body = await Page.Locator("body").InnerTextAsync();
            if (body.Contains("Um erro inesperado", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("An unhandled exception", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Erro interno", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{route}: página de erro renderizada");
        }

        return errors;
    }

    // ------------------------------------------------------------------
    // Fluxo real de empresa: registro → perfil → criar vaga → vaga listada
    // ------------------------------------------------------------------

    [Fact]
    public async Task Flow_Advertiser_Register_Profile_CreateJob()
    {
        var email = $"empresa-real-{Guid.NewGuid():N}@test.com";
        await RegisterAdvertiserAsync(email);

        // Perfil da empresa
        var response = await GotoAsync("/profile/advertiser");
        Assert.True(response!.Ok, $"AdvertiserProfile retornou {response?.Status}");
        await WaitForBlazorAsync();
        var name = await Page.GetByTestId("advertiser-profile-name").InputValueAsync();
        Assert.Equal("Empresa E2E Real", name);

        // Criar vaga
        await GotoAsync("/jobs/create");
        await WaitForBlazorAsync();

        var jobTitle = $"Vaga Real E2E {DateTime.Now:HHmmss}";
        await Page.GetByTestId("job-title-input").FillAsync(jobTitle);
        await Page.GetByTestId("job-description-input")
            .FillAsync("Vaga criada na bateria de varredura real.");
        await Page.GetByTestId("job-type-select").SelectOptionAsync("Remote");

        var submit = Page.GetByTestId("job-create-submit");
        Assert.True(await submit.IsEnabledAsync(), "Botão de criar vaga deveria estar habilitado");
        await submit.ClickAsync();

        var modal = Page.GetByTestId("job-details-modal");
        await modal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal(jobTitle, (await modal.Locator("#job-modal-title").TextContentAsync())?.Trim());

        // Vaga aparece em "Minhas vagas"
        await GotoAsync("/my-jobs");
        await WaitForBlazorAsync();
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains(jobTitle, body, StringComparison.OrdinalIgnoreCase);

        // E na busca pública
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();
        await Page.GetByTestId("job-search").FillAsync(jobTitle);
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.WaitForFunctionAsync(
            "t => document.querySelector('.job-card__title')?.textContent?.trim() === t",
            jobTitle);
    }

    // ------------------------------------------------------------------
    // Fluxo real de candidato: registro → perfil → candidatura → status
    // ------------------------------------------------------------------

    [Fact]
    public async Task Flow_Candidate_Register_Profile_Apply()
    {
        var email = $"cand-real-{Guid.NewGuid():N}@test.com";
        await LoginOrRegisterAsync(email, TestPassword);

        // Perfil
        var response = await GotoAsync("/profile/candidate");
        Assert.True(response!.Ok, $"CandidateProfile retornou {response?.Status}");
        await WaitForBlazorAsync();
        var name = await Page.GetByTestId("candidate-profile-name").InputValueAsync();
        Assert.Equal("Pessoa Teste E2E", name);

        // Candidatura na primeira vaga disponível
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();
        await Page.GetByTestId("job-card-details").First.ClickAsync();
        await Page.GetByTestId("job-details-modal").WaitForAsync();
        await Page.GetByTestId("job-modal-apply").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.pathname.startsWith('/apply/')");
        await WaitForBlazorAsync();

        var submit = Page.GetByTestId("application-submit");
        Assert.True(await submit.IsEnabledAsync(), "Botão de candidatura deveria estar habilitado");
        await Page.GetByTestId("application-notes").FillAsync("Candidatura da varredura real");
        await submit.ClickAsync();

        await Page.GetByTestId("application-feedback")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 45000 });

        // Aparece em "Minhas candidaturas"
        await GotoAsync("/my-applications");
        await WaitForBlazorAsync();
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("Nenhuma candidatura", body, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task RegisterAdvertiserAsync(string email)
    {
        await GotoAsync("/Account/Register");
        await Page.GetByTestId("register-email").WaitForAsync();
        await Page.GetByTestId("register-name").FillAsync("Empresa E2E Real");
        await Page.GetByTestId("register-role-advertiser").CheckAsync();
        await Page.GetByTestId("register-email").FillAsync(email);
        await Page.GetByTestId("register-password").FillAsync(TestPassword);
        await Page.GetByTestId("register-confirm-password").FillAsync(TestPassword);
        await Page.GetByTestId("register-submit").ClickAsync();
        await Page.WaitForFunctionAsync("""
            () => !!document.querySelector('[data-testid="nav-jobs"]') ||
                  !!document.querySelector('[data-testid="account-status"]')
            """);
        Assert.DoesNotContain("/Account/Register", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
