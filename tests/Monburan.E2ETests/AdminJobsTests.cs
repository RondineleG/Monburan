using System.Text.Json;

using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>Moderação de vagas pela interface: tirar do ar com motivo, a empresa ver o motivo, voltar e excluir.</summary>
public class AdminJobsTests : E2ETestBase
{
    private const string AdminEmail = "admin@monburan.com.br";
    private const string JobsPath = "/admin/vagas";

    public AdminJobsTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private ILocator Row(string title) => Page.Locator($"[data-testid='job-row'][data-title='{title}']");

    private async Task OpenJobsAsync()
    {
        await GotoAsync(JobsPath);
        await WaitForBlazorAsync();
        await Page.GetByTestId("jobs-search").WaitForAsync();
    }

    private async Task ManageAsync(string title)
    {
        await Page.GetByTestId("jobs-search").FillAsync(title);
        await Page.GetByTestId("jobs-apply").ClickAsync();
        await Row(title).GetByTestId("job-manage").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-panel")).ToBeVisibleAsync();
    }

    /// <summary>Publica uma vaga pela API como a empresa de teste e devolve o id.</summary>
    private async Task<string> CreateJobAsCompanyAsync(string title)
    {
        var api = await Fixture.Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions { BaseURL = BaseUrl, IgnoreHTTPSErrors = true });
        try
        {
            var login = await api.PostAsync("/api/auth/login", new APIRequestContextOptions { DataObject = new { email = AdvertiserEmail, password = TestPassword } });
            Assert.True(login.Ok, "A empresa de teste deveria entrar pela API");
            var token = (await login.JsonAsync())!.Value.GetProperty("accessToken").GetString();

            var created = await api.PostAsync("/api/v1/jobs", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
                DataObject = new
                {
                    title,
                    description = "Vaga criada pelo teste E2E de moderação.",
                    expirationDate = DateTime.UtcNow.AddDays(20),
                    numberOfOpenings = 1,
                    type = "Remote",
                    isRemote = true,
                    salaryCurrency = "BRL",
                    salaryDisplay = true
                }
            });
            Assert.True(created.Status == 201, "Não foi possível publicar a vaga de teste: " + await created.TextAsync());
            return (await created.JsonAsync())!.Value.GetProperty("id").GetString()!;
        }
        finally
        {
            await api.DisposeAsync();
        }
    }

    private async Task<int> PublicStatusOfAsync(string jobId)
    {
        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{BaseUrl}/jobs/{jobId}");
        return response!.Status;
    }

    [Fact]
    public async Task Admin_HidesAJobWithAReason_TheCompanySeesIt_AndTheAdminBringsItBack()
    {
        var title = "E2E Moderação " + Guid.NewGuid().ToString("N")[..8];
        var jobId = await CreateJobAsCompanyAsync(title);
        Assert.Equal(200, await PublicStatusOfAsync(jobId));

        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenJobsAsync();
        await ManageAsync(title);

        // Ocultar exige motivo
        await Page.GetByTestId("job-hide").ClickAsync();
        await Page.GetByTestId("job-action-confirm").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-reason-error")).ToContainTextAsync("motivo");
        await Page.GetByTestId("job-reason").FillAsync("Descrição sem as responsabilidades da função");
        await Page.GetByTestId("job-action-confirm").ClickAsync();

        await Assertions.Expect(Page.GetByTestId("job-panel-state")).ToHaveTextAsync("Oculta");
        await Assertions.Expect(Page.GetByTestId("job-panel-moderation")).ToContainTextAsync("Descrição sem as responsabilidades da função");
        Assert.Equal(404, await PublicStatusOfAsync(jobId));

        // A empresa vê o motivo na própria lista
        await using (var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true }))
        {
            var company = await context.NewPageAsync();
            company.SetDefaultTimeout(20000);
            await company.GotoAsync(BaseUrl + "/entrar");
            await company.GetByTestId("login-email").FillAsync(AdvertiserEmail);
            await company.GetByTestId("login-password").FillAsync(TestPassword);
            await company.GetByTestId("login-submit").ClickAsync();
            await company.WaitForURLAsync(url => !url.Contains("/entrar", StringComparison.OrdinalIgnoreCase));
            await company.GotoAsync(BaseUrl + "/minhas-vagas?situacao=ocultas");
            await Assertions.Expect(company.GetByTestId("my-job-moderation").First).ToContainTextAsync("Descrição sem as responsabilidades da função");
        }

        // O administrador abre a página da vaga mesmo oculta
        var opened = await Page.APIRequest.GetAsync($"{BaseUrl}/jobs/{jobId}");
        Assert.Equal(200, opened.Status);

        // Aprovar e ativar: a vaga volta ao site
        await Page.GetByTestId("job-activate").ClickAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-panel-state")).ToHaveTextAsync("Ativa");
        await Assertions.Expect(Page.GetByTestId("job-panel-moderation")).ToHaveCountAsync(0);
        Assert.Equal(200, await PublicStatusOfAsync(jobId));

        // Excluir também exige motivo e tira a vaga da lista
        await Page.GetByTestId("job-delete").ClickAsync();
        await Page.GetByTestId("job-reason").FillAsync("Vaga de teste removida");
        await Page.GetByTestId("job-action-confirm").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-panel")).ToHaveCountAsync(0);
        await Assertions.Expect(Row(title)).ToHaveCountAsync(0);
        Assert.Equal(404, await PublicStatusOfAsync(jobId));

        // Tudo ficou na auditoria
        await GotoAsync("/admin/auditoria");
        await WaitForBlazorAsync();
        await Page.GetByTestId("audit-search").FillAsync(title);
        await Page.GetByTestId("audit-apply").ClickAsync();
        var rows = Page.GetByTestId("audit-row");
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Situação da vaga alterada" })).ToHaveCountAsync(2);
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Vaga excluída" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Admin_PausesAndRejects_AndTheCountersFollow()
    {
        var title = "E2E Rejeição " + Guid.NewGuid().ToString("N")[..8];
        var jobId = await CreateJobAsCompanyAsync(title);
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenJobsAsync();
        var rejectedBefore = int.Parse(await Page.GetByTestId("jobs-count-rejected").InnerTextAsync());

        await ManageAsync(title);
        await Page.GetByTestId("job-pause").ClickAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-panel-state")).ToHaveTextAsync("Pausada");
        Assert.Equal(404, await PublicStatusOfAsync(jobId));

        await Page.GetByTestId("job-reject").ClickAsync();
        await Page.GetByTestId("job-reason").FillAsync("Anúncio de curso pago disfarçado de vaga");
        await Page.GetByTestId("job-action-confirm").ClickAsync();
        await Assertions.Expect(Page.GetByTestId("job-panel-state")).ToHaveTextAsync("Reprovada");
        await Assertions.Expect(Page.GetByTestId("jobs-count-rejected")).ToHaveTextAsync((rejectedBefore + 1).ToString());

        // Filtro por situação
        await Page.GetByTestId("jobs-status").SelectOptionAsync("Rejected");
        await Page.GetByTestId("jobs-apply").ClickAsync();
        await Assertions.Expect(Row(title)).ToBeVisibleAsync();
        await Page.GetByTestId("jobs-status").SelectOptionAsync("Active");
        await Page.GetByTestId("jobs-apply").ClickAsync();
        await Assertions.Expect(Row(title)).ToHaveCountAsync(0);

        // Limpa: exclui a vaga de teste
        await Page.GetByTestId("jobs-status").SelectOptionAsync("");
        await ManageAsync(title);
        await Page.GetByTestId("job-delete").ClickAsync();
        await Page.GetByTestId("job-reason").FillAsync("Limpeza do teste E2E");
        await Page.GetByTestId("job-action-confirm").ClickAsync();
        await Assertions.Expect(Row(title)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Admin_SeesTheMenuLink_AndOthersCannotOpenThePage()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await GotoAsync("/");
        await WaitForBlazorAsync();
        await Assertions.Expect(Page.GetByTestId("nav-admin-jobs")).ToBeVisibleAsync();

        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var company = await context.NewPageAsync();
        company.SetDefaultTimeout(20000);
        await company.GotoAsync(BaseUrl + "/entrar");
        await company.GetByTestId("login-email").FillAsync(AdvertiserEmail);
        await company.GetByTestId("login-password").FillAsync(TestPassword);
        await company.GetByTestId("login-submit").ClickAsync();
        await company.WaitForURLAsync(url => !url.Contains("/entrar", StringComparison.OrdinalIgnoreCase));

        await company.GotoAsync(BaseUrl + JobsPath);

        Assert.Contains("AccessDenied", company.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await company.GetByTestId("job-row").CountAsync());
    }

    [Fact]
    public async Task Anonymous_IsSentToTheLogin()
    {
        await GotoAsync(JobsPath);

        Assert.Contains("/entrar", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
