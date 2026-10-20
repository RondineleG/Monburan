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

        // A vaga tem página própria: o cartão leva a ela e de lá o candidato segue para o formulário
        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-detail-title").WaitForAsync();

        await Page.GetByTestId("job-apply").ClickAsync();
        await Page.WaitForFunctionAsync(
            "() => location.pathname.startsWith('/apply/')",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });
        await WaitForBlazorAsync();

        await Page.GetByTestId("application-notes")
            .FillAsync("Candidatura E2E automatizada");
        await Page.GetByTestId("application-continue").ClickAsync();
        await Page.GetByTestId("application-confirm").CheckAsync();
        await WaitForBlazorAsync();

        var submit = Page.GetByTestId("application-submit");
        // Espera o circuito liberar o botão depois da confirmação: a leitura instantânea perdia a corrida no CI
        await Assertions.Expect(submit).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 20000 });
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
        // Vaga "Desenvolvedor Full Stack Pleno" já tem candidatura do candidato seedado:
        // a página da vaga oferece acompanhamento (não candidatura) e /apply barra de cara.
        await LoginAsCandidateAsync();
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        await Page.GetByTestId("job-search").FillAsync("Desenvolvedor Full Stack Pleno");
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('.job-card__title')?.textContent?.trim() === 'Desenvolvedor Full Stack Pleno'");
        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-detail-title").WaitForAsync();
        await WaitForBlazorAsync();

        var jobId = new Uri(Page.Url).Segments.Last();
        await Page.GetByTestId("job-track-application").WaitForAsync();
        Assert.Equal(0, await Page.GetByTestId("job-apply").CountAsync());

        // Voltar direto à URL de candidatura mostra a situação, nunca o formulário de novo
        await GotoAsync($"/apply/{jobId}");
        await WaitForBlazorAsync();
        var already = Page.GetByTestId("application-already");
        await already.WaitForAsync();
        Assert.Contains("já se candidatou", await already.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.GetByTestId("application-continue").CountAsync());
        Assert.Equal(0, await Page.GetByTestId("application-submit").CountAsync());

        // No celular a barra fixa também oferece acompanhamento
        await Page.SetViewportSizeAsync(390, 844);
        await GotoAsync($"/jobs/{jobId}");
        await WaitForBlazorAsync();
        await Page.GetByTestId("job-track-application-mobile").WaitForAsync();
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
