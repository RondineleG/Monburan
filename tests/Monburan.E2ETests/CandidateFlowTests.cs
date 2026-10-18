using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class CandidateFlowTests : E2ETestBase
{
    public CandidateFlowTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private async Task LoginAsCandidateAsync() =>
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);

    [Fact]
    public async Task Candidate_Dashboard_ShouldShowCareerContentOnly()
    {
        await LoginAsCandidateAsync();
        await GotoAsync("/dashboard");
        await WaitForBlazorAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("SUA JORNADA", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SEU ESPAÇO MONBURAN", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Total de vagas", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Minhas candidaturas", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Candidate_Profile_ShouldLoad_SeededData()
    {
        await LoginAsCandidateAsync();

        var response = await GotoAsync("/profile/candidate");
        Assert.True(response!.Ok, $"Profile retornou {response.Status}");

        await WaitForBlazorAsync();
        var name = await Page.GetByTestId("candidate-profile-name").InputValueAsync();
        Assert.Equal("João Silva", name);
    }

    [Fact]
    public async Task Candidate_Navigation_ShouldShowCareerLinksOnly()
    {
        await LoginAsCandidateAsync();
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        Assert.True(await Page.GetByTestId("nav-applications").IsVisibleAsync());
        Assert.Equal(0, await Page.GetByTestId("nav-my-jobs").CountAsync());
    }

    [Fact]
    public async Task Candidate_MyApplications_ShouldLoad()
    {
        await LoginAsCandidateAsync();

        var response = await GotoAsync("/my-applications");
        Assert.True(response!.Ok, $"MyApplications retornou {response.Status}");

        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task Candidate_Can_ApplyToJob()
    {
        var candidateEmail = $"candidatura-{Guid.NewGuid():N}@test.com";
        await LoginOrRegisterAsync(candidateEmail, TestPassword);

        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        var detailsButton = Page.GetByTestId("job-card-details").First;
        await detailsButton.ClickAsync();
        await Page.GetByTestId("job-details-modal").WaitForAsync();

        await Page.GetByTestId("job-modal-apply").ClickAsync();
        try
        {
            await Page.WaitForFunctionAsync(
                "() => location.pathname.startsWith('/apply/')",
                null,
                new PageWaitForFunctionOptions { Timeout = 30000 });
        }
        catch (TimeoutException)
        {
            var modalOpen = await Page.GetByTestId("job-details-modal").CountAsync();
            var applyVisible = await Page.GetByTestId("job-modal-apply").IsVisibleAsync();
            var ready = await Page.EvaluateAsync<string?>(
                "() => document.querySelector('[data-testid=\"app-shell\"]')?.getAttribute('data-blazor-ready')");
            throw new Exception(
                $"Navegação para /apply falhou. url={Page.Url} modalOpen={modalOpen} applyVisible={applyVisible} blazorReady={ready}");
        }
        await WaitForBlazorAsync();

        var submit = Page.GetByTestId("application-submit");
        Assert.True(await submit.IsEnabledAsync(),
            "Botão de candidatura deveria estar habilitado");

        await Page.GetByTestId("application-notes")
            .FillAsync("Candidatura E2E automatizada");
        await submit.ClickAsync();
        await Page.GetByTestId("application-feedback").WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 45000
        });

        Assert.True(await Page.GetByTestId("application-feedback").IsVisibleAsync(),
            "Nenhum feedback exibido após enviar candidatura");
    }

    [Fact]
    public async Task Candidate_DuplicateApplication_ShouldShowFriendlyFeedback()
    {
        await LoginAsCandidateAsync();
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        await Page.GetByTestId("job-search").FillAsync("Desenvolvedor Full Stack Pleno");
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('.job-card__title')?.textContent?.trim() === 'Desenvolvedor Full Stack Pleno'")
            ;
        await Page.GetByTestId("job-card-details").First.ClickAsync();
        await Page.GetByTestId("job-details-modal").WaitForAsync();
        await Page.GetByTestId("job-modal-apply").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.pathname.startsWith('/apply/')");
        await WaitForBlazorAsync();
        await Page.GetByTestId("application-submit").ClickAsync();
        await Page.GetByTestId("application-feedback").WaitForAsync();

        Assert.Contains("já se candidatou", await Page.GetByTestId("application-feedback").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Candidate_Resumes_ShouldLoad()
    {
        await LoginAsCandidateAsync();

        var response = await GotoAsync("/resumes");
        Assert.True(response!.Ok, $"Resumes retornou {response.Status}");
        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task Candidate_ResumeCreate_ShouldRenderForm()
    {
        await LoginAsCandidateAsync();

        var response = await GotoAsync("/resumes/new");
        Assert.True(response!.Ok, $"ResumeCreate retornou {response.Status}");

        await WaitForBlazorAsync();
        var submit = Page.GetByTestId("resume-create-submit");
        Assert.True(await submit.IsVisibleAsync(),
            "Formulário de criação de currículo não renderizou");
    }

    [Fact]
    public async Task Candidate_Notifications_ShouldLoad()
    {
        await LoginAsCandidateAsync();

        var response = await GotoAsync("/notifications");
        Assert.True(response!.Ok, $"Notifications retornou {response.Status}");
        await WaitForBlazorAsync();
    }

    // Plano e assinatura são da conta da empresa (AdvertiserOnly): o candidato cai na tela de acesso restrito
    [Fact]
    public async Task Candidate_MySubscription_IsRestrictedToAdvertisers()
    {
        await LoginAsCandidateAsync();

        await GotoAsync("/my-subscription");
        await WaitForBlazorAsync();

        Assert.Contains("/Account/AccessDenied", Page.Url, StringComparison.OrdinalIgnoreCase);
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Esta área é restrita", body, StringComparison.OrdinalIgnoreCase);
    }
}
