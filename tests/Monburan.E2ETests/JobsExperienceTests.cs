using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>
/// Busca de vagas (filtros, ordenação e endereço compartilhável) e página pública da vaga, vistas por quem
/// ainda não entrou na conta.
/// </summary>
public class JobsExperienceTests : E2ETestBase
{
    public JobsExperienceTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private async Task OpenJobsAsync(string path = "/jobs")
    {
        await GotoAsync(path);
        await WaitForBlazorAsync();
    }

    private async Task SearchAsync(string term)
    {
        await Page.GetByTestId("job-search").FillAsync(term);
        await Page.GetByTestId("job-search-submit").ClickAsync();
        await Page.WaitForFunctionAsync($"() => location.search === '?q={term}'");
    }

    private async Task OpenFirstJobAsync()
    {
        // O cartão inteiro é o alvo do clique (o link do título o cobre): clicar no centro dele abre a vaga
        await Page.GetByTestId("job-card").First.ClickAsync();
        await Page.GetByTestId("job-detail-title").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });
    }

    [Fact]
    public async Task Jobs_Search_ShouldPutOnlyTheUsedFiltersInUrl_AndSurviveReload()
    {
        await OpenJobsAsync();

        await SearchAsync("Desenvolvedor");
        await Page.WaitForFunctionAsync("""
            () => {
                const cards = Array.from(document.querySelectorAll('[data-testid="job-card"]'));
                return cards.length > 0 && cards.every(card => card.textContent.toLowerCase().includes('desenvolvedor'));
            }
            """);

        await Page.ReloadAsync();
        await WaitForBlazorAsync();

        Assert.Equal("Desenvolvedor", await Page.GetByTestId("job-search").InputValueAsync());
        var cards = await Page.GetByTestId("job-card").AllTextContentsAsync();
        Assert.NotEmpty(cards);
        Assert.All(cards, text => Assert.Contains("desenvolvedor", text, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Desenvolvedor", await Page.TitleAsync());
    }

    [Fact]
    public async Task Jobs_ModeFilter_ShouldNarrowResults_AndBeClearable()
    {
        await OpenJobsAsync();

        await Page.GetByTestId("filter-mode-OnSite").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.search === '?modo=OnSite'");

        // O resumo do filtro só aparece depois de a lista nova ser renderizada
        var context = Page.GetByTestId("jobs-context");
        await context.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("Presencial", await context.InnerTextAsync());
        Assert.All(await Page.GetByTestId("job-card").AllTextContentsAsync(),
            text => Assert.Contains("Presencial", text));

        await Page.GetByTestId("filters-clear").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.pathname === '/jobs' && location.search === ''");
        await WaitForBlazorAsync();

        Assert.Equal(0, await Page.GetByTestId("jobs-context").CountAsync());
        Assert.True(await Page.Locator("input[name='modo'][value='']").IsCheckedAsync(),
            "Depois de limpar os filtros, 'Todas' deveria estar marcado");
    }

    [Fact]
    public async Task Jobs_SalaryFloor_ShouldHideLowerPaidJobs()
    {
        await OpenJobsAsync();

        await Page.GetByTestId("filter-salary").SelectOptionAsync("8000");
        await Page.WaitForFunctionAsync("() => location.search === '?salario=8000'");
        await Page.GetByTestId("jobs-context").WaitForAsync();   // só existe depois que a lista filtrada é renderizada

        // Quem aparece informa uma faixa cujo teto alcança o piso escolhido
        var maximums = await Page.EvaluateAsync<double[]>("""
            () => Array.from(document.querySelectorAll('[data-testid="job-card"] .job-facts__salary'))
                .map(el => Math.max(...(el.textContent.match(/\d[\d.]*/g) || ['0']).map(n => parseFloat(n.replace(/\./g, '')))))
            """);
        Assert.All(maximums, max => Assert.True(max >= 8000, $"Vaga com teto {max} apareceu num filtro de R$ 8.000"));
    }

    [Fact]
    public async Task Jobs_SortBySalary_ShouldListHigherSalariesFirst_AndJobsWithoutSalaryLast()
    {
        await OpenJobsAsync();

        await Page.GetByTestId("jobs-sort").SelectOptionAsync("salario");
        await Page.WaitForFunctionAsync("() => location.search === '?ordem=salario'");
        // O bloco de resultados carrega a ordenação com que foi renderizado: só então a lista é a nova
        await Page.Locator("[data-testid='jobs-results'][data-sort='salario']").WaitForAsync();

        // Um teto salarial por cartão; null quando a vaga não informa
        var ceilings = await Page.EvaluateAsync<double?[]>("""
            () => Array.from(document.querySelectorAll('[data-testid="job-card"]')).map(card => {
                const el = card.querySelector('.job-facts__salary');
                if (!el) return null;
                return Math.max(...(el.textContent.match(/\d[\d.]*/g) || ['0']).map(n => parseFloat(n.replace(/\./g, ''))));
            })
            """);

        var withSalary = ceilings.TakeWhile(value => value.HasValue).Select(value => value!.Value).ToList();
        Assert.NotEmpty(withSalary);
        Assert.Equal(withSalary.OrderByDescending(value => value), withSalary);
        Assert.All(ceilings.Skip(withSalary.Count), value => Assert.Null(value));
    }

    [Fact]
    public async Task Jobs_LegacyJobIdLink_ShouldRedirectToTheJobPage()
    {
        await OpenJobsAsync();
        var href = await Page.GetByTestId("job-card-link").First.GetAttributeAsync("href");
        Assert.NotNull(href);

        await GotoAsync($"/jobs?jobId={href!.Split('/').Last()}");

        await Page.GetByTestId("job-detail-title").WaitForAsync();
        Assert.Equal(href, new Uri(Page.Url).AbsolutePath);
    }

    [Fact]
    public async Task Jobs_BackButton_ShouldRestorePreviousSearchAndFields()
    {
        await OpenJobsAsync();
        await SearchAsync("Desenvolvedor");

        await Page.GetByTestId("filter-mode-Remote").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.search === '?q=Desenvolvedor&modo=Remote'");

        await Page.GoBackAsync();

        await Page.WaitForFunctionAsync("""
            () => location.search === '?q=Desenvolvedor'
                && document.querySelector('input[name="modo"]:checked')?.value === ''
            """);
        Assert.Equal("Desenvolvedor", await Page.GetByTestId("job-search").InputValueAsync());
    }

    [Fact]
    public async Task JobDetails_BackToList_ShouldKeepTheSearch()
    {
        await OpenJobsAsync();
        await SearchAsync("Desenvolvedor");
        await OpenFirstJobAsync();

        await Page.Locator("[data-action='back-to-list']").ClickAsync();

        await Page.WaitForFunctionAsync("() => location.pathname === '/jobs' && location.search === '?q=Desenvolvedor'");
        await Page.GetByTestId("job-card").First.WaitForAsync();
        Assert.Equal("Desenvolvedor", await Page.GetByTestId("job-search").InputValueAsync());
    }

    [Fact]
    public async Task JobDetails_Public_ShouldServeStructuredDataAndShareTagsToSearchEngines()
    {
        var api = await Fixture.Playwright.APIRequest.NewContextAsync();
        try
        {
            var list = await (await api.GetAsync($"{BaseUrl}/jobs")).TextAsync();
            var match = Regex.Match(list, "href=\"/jobs/(?<id>[0-9a-f-]{36})\"");
            Assert.True(match.Success, "A lista pública deveria ter links para as vagas");

            var response = await api.GetAsync($"{BaseUrl}/jobs/{match.Groups["id"].Value}");
            Assert.Equal(200, response.Status);
            var html = await response.TextAsync();

            // O Blazor escreve o "+" do tipo como &#x2B;; o navegador (e o buscador) decodifica do mesmo jeito
            var script = Regex.Match(html, "<script type=\"application/ld(?:\\+|&#x2B;)json\">(?<json>.*?)</script>", RegexOptions.Singleline);
            Assert.True(script.Success, "A página da vaga deveria trazer os dados estruturados JobPosting");
            using var data = JsonDocument.Parse(script.Groups["json"].Value);
            Assert.Equal("JobPosting", data.RootElement.GetProperty("@type").GetString());
            Assert.False(string.IsNullOrWhiteSpace(data.RootElement.GetProperty("title").GetString()));

            Assert.Contains("property=\"og:title\"", html);
            Assert.Contains("rel=\"canonical\"", html);
            Assert.DoesNotContain("noindex", html);
        }
        finally
        {
            await api.DisposeAsync();
        }
    }

    [Fact]
    public async Task JobDetails_Unknown_ShouldReturn404_WithAWayBackToTheJobs()
    {
        var response = await GotoAsync($"/jobs/{Guid.NewGuid()}");

        // Status 404 de verdade (buscadores deixam de indexar) e uma tela que leva de volta às vagas, não uma página em branco
        Assert.NotNull(response);
        Assert.Equal(404, response!.Status);
        await Page.GetByTestId("job-not-found").WaitForAsync();
        Assert.True(await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Ver vagas abertas" }).IsVisibleAsync());
        Assert.Contains("noindex", await response.TextAsync());
    }

    [Fact]
    public async Task JobDetails_CopyLink_ShouldPutTheJobUrlInTheClipboard()
    {
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await OpenJobsAsync();
        await OpenFirstJobAsync();

        await Page.GetByTestId("job-share-copy").ClickAsync();

        await Page.WaitForFunctionAsync("""
            () => document.querySelector('[data-testid="job-share-copy"] [data-copy-label]')?.textContent.trim() === 'Link copiado'
            """);
        var copied = await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Equal(Page.Url.Split('#')[0], copied);
        Assert.Contains("https://wa.me/", await Page.GetByTestId("job-share-whatsapp").GetAttributeAsync("href"));
    }

    [Fact]
    public async Task JobDetails_Mobile_ShouldShowStickyApplyBar_WithoutHorizontalOverflow()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await OpenJobsAsync();
        await OpenFirstJobAsync();

        Assert.True(await Page.GetByTestId("job-apply-mobile").IsVisibleAsync(),
            "No celular a candidatura fica numa barra fixa");
        Assert.False(await Page.GetByTestId("job-apply").IsVisibleAsync(),
            "O cartão lateral de candidatura é escondido no celular (a barra fixa o substitui)");

        var fits = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= document.documentElement.clientWidth");
        Assert.True(fits, "A página da vaga ultrapassou a largura do celular");

        await Page.GetByTestId("job-apply-mobile").ClickAsync();
        await Page.WaitForFunctionAsync("() => location.pathname === '/Account/Login'");
        Assert.Contains("ReturnUrl=%2Fapply%2F", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
