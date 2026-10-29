using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class PublicPagesTests : E2ETestBase
{
    public PublicPagesTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Home_ShouldLoadDashboard()
    {
        var response = await GotoAsync("/");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Home retornou {response.Status}");

        await WaitForBlazorAsync();
        Assert.Contains("Monburan", await Page.TitleAsync());
    }

    [Fact]
    public async Task AnonymousHome_ShouldShowPublicLandingWithoutRecruiterMetrics()
    {
        await GotoAsync("/");
        await WaitForBlazorAsync();

        // Visitante: busca primeiro, cabeçalho público, sem menu lateral nem indicadores de recrutamento
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.True(await Page.GetByTestId("landing-search").IsVisibleAsync(), "A página inicial do visitante começa pela busca");
        Assert.Contains("Buscar vagas", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Total de vagas", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.Locator(".app-sidebar").CountAsync());
        Assert.Equal(1, await Page.Locator(".public-header").CountAsync());
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        var api = await Fixture.Playwright.APIRequest.NewContextAsync();
        var response = await api.GetAsync($"{BaseUrl}/health");
        Assert.True(response.Ok);
    }

    [Fact]
    public async Task Jobs_ShouldList_SeededJobs()
    {
        var response = await GotoAsync("/jobs");
        Assert.True(response!.Ok, $"Jobs retornou {response.Status}");

        await WaitForBlazorAsync();

        var cards = Page.GetByTestId("job-card");
        await cards.First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });

        Assert.True(await cards.CountAsync() > 0,
            "Nenhuma vaga listada — seed deveria criar vagas");
    }

    [Fact]
    public async Task Jobs_Search_ShouldFilterResults()
    {
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        await Page.GetByTestId("job-search")
            .FillAsync("Desenvolvedor");
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.WaitForFunctionAsync("""
            () => {
                const cards = Array.from(document.querySelectorAll('[data-testid="job-card"]'));
                return cards.length > 0 && cards.every(card => card.textContent.toLowerCase().includes("desenvolvedor"));
            }
            """);

        var cards = Page.GetByTestId("job-card");
        Assert.True(await cards.CountAsync() > 0,
            "Busca por 'Desenvolvedor' não retornou vagas");

        var titles = await cards.AllTextContentsAsync();
        Assert.All(titles, t => Assert.Contains("Desenvolvedor", t, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Jobs_Search_NoMatch_ShouldShowEmptyState()
    {
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        await Page.GetByTestId("job-search")
            .FillAsync("xyznonexistentterm");
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.GetByTestId("jobs-empty").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });

        Assert.True(await Page.GetByTestId("jobs-empty")
            .IsVisibleAsync(),
            "Estado vazio não exibido para busca sem resultados");
    }

    [Fact]
    public async Task Jobs_Mobile_ShouldFitViewport_WithoutHorizontalOverflow()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        var layoutFits = await Page.EvaluateAsync<bool>("""
            () => {
                const width = document.documentElement.clientWidth;
                const selectors = [".app-topbar", ".jobs-page", ".jobs-search__bar", ".jobs-grid", ".job-card"];
                return selectors.every(selector => Array.from(document.querySelectorAll(selector)).every(element => {
                    const bounds = element.getBoundingClientRect();
                    return bounds.left >= -1 && bounds.right <= width + 1;
                }));
            }
            """);

        Assert.True(layoutFits, "Um dos principais elementos da página ultrapassou a viewport mobile.");
        Assert.True(await Page.GetByTestId("job-search").IsVisibleAsync());
    }

    [Fact]
    public async Task MobileNavigation_ShouldOpenAndClose()
    {
        // O menu lateral (e o botão que o abre no celular) é da área logada; o visitante usa o cabeçalho público
        await LoginOrRegisterAsync(CandidateEmail, TestPassword);
        await Page.SetViewportSizeAsync(390, 844);
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        var menuButton = Page.GetByTestId("mobile-menu-toggle");
        Assert.True(await menuButton.IsVisibleAsync());
        await menuButton.ClickAsync();

        var sidebar = Page.GetByTestId("app-sidebar");
        Assert.Contains("app-sidebar--open", (await sidebar.GetAttributeAsync("class")) ?? string.Empty);
        await Page.GetByTestId("nav-jobs").ClickAsync();
        Assert.DoesNotContain("app-sidebar--open", (await sidebar.GetAttributeAsync("class")) ?? string.Empty);
    }

    [Theory]
    [InlineData("/entrar")]
    [InlineData("/criar-conta")]
    public async Task AccountPages_Mobile_ShouldFitViewport(string path)
    {
        await Page.SetViewportSizeAsync(390, 844);
        await GotoAsync(path);
        await WaitForBlazorAsync();

        var viewportWidth = await Page.EvaluateAsync<int>("() => document.documentElement.clientWidth");
        var documentWidth = await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
        Assert.True(documentWidth <= viewportWidth, $"{path} ultrapassou a viewport: {documentWidth}px > {viewportWidth}px");
    }

    [Fact]
    public async Task JobDetails_Anonymous_ShouldOpenPublicPage_WithoutLogin()
    {
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-detail-title").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });

        Assert.Matches("^/jobs/[0-9a-f-]{36}$", new Uri(Page.Url).AbsolutePath);
        Assert.True(await Page.GetByTestId("job-apply").IsVisibleAsync(),
            "O visitante deveria ver o botão para se candidatar");
    }

    [Fact]
    public async Task JobDetails_Anonymous_Apply_ShouldRedirectToLogin_WithReturnUrl()
    {
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();
        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-detail-title").WaitForAsync();
        var jobId = new Uri(Page.Url).AbsolutePath.Split('/').Last();

        await Page.GetByTestId("job-apply").ClickAsync();
        await Page.WaitForFunctionAsync(
            "() => location.pathname === '/entrar'",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        // Depois do login a pessoa volta direto para o formulário de candidatura
        Assert.Contains($"ReturnUrl=%2Fapply%2F{jobId}", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JobDetails_Anonymous_AfterLogin_ShouldOpenApplyPage()
    {
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();
        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-apply").ClickAsync();
        await Page.WaitForFunctionAsync(
            "() => location.pathname === '/entrar'",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        await Page.GetByTestId("login-email").FillAsync(CandidateEmail);
        await Page.GetByTestId("login-password").FillAsync(TestPassword);
        await Page.GetByTestId("login-submit").ClickAsync();

        await Page.WaitForFunctionAsync(
            "() => location.pathname.startsWith('/candidatar/')",
            null,
            new PageWaitForFunctionOptions { Timeout = 45000 });
        await WaitForBlazorAsync();

        // O formulário abre na etapa 1 (revisão); a etapa 2 traz o envio da candidatura. Se outro teste já se
        // candidatou à mesma vaga, a página mostra "Você já se candidatou" — também é a página de candidatura certa.
        var reviewOrDone = Page.Locator("[data-testid='application-continue'], [data-testid='application-already']");
        await reviewOrDone.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
        if (await Page.GetByTestId("application-continue").CountAsync() > 0)
        {
            await Page.GetByTestId("application-continue").ClickAsync();
            await Page.GetByTestId("application-submit").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30000
            });
        }
    }

    [Fact]
    public async Task Plans_ShouldList_SeededPlans()
    {
        await LoginOrRegisterAsync(AdvertiserEmail, TestPassword);
        var response = await GotoAsync("/plans");
        Assert.True(response!.Ok, $"Plans retornou {response.Status}");

        await WaitForBlazorAsync();
        var content = await Page.ContentAsync();
        Assert.Contains("Premium", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/privacidade")]
    [InlineData("/termos")]
    [InlineData("/enderecos")]
    public async Task StaticPages_ShouldReturnOk(string path)
    {
        var response = await GotoAsync(path);
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"{path} retornou {response.Status}");
    }
}
