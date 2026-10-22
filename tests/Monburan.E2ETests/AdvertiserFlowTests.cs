using Microsoft.Playwright;

namespace Monburan.E2ETests;

public class AdvertiserFlowTests : E2ETestBase
{
    public AdvertiserFlowTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private async Task LoginAsAdvertiserAsync() =>
        await LoginOrRegisterAsync(AdvertiserEmail, TestPassword);

    [Fact]
    public async Task Advertiser_Profile_ShouldLoad_SeededData()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/profile/advertiser");
        Assert.True(response!.Ok, $"AdvertiserProfile retornou {response.Status}");

        await WaitForBlazorAsync();
        var name = await Page.GetByTestId("advertiser-profile-name").InputValueAsync();
        Assert.Equal("Tech Solutions Ltda", name);
    }

    [Fact]
    public async Task Advertiser_Navigation_ShouldShowRecruitmentLinksOnly()
    {
        await LoginAsAdvertiserAsync();
        await GotoAsync("/jobs");
        await WaitForBlazorAsync();

        Assert.True(await Page.GetByTestId("nav-my-jobs").IsVisibleAsync());
        Assert.True(await Page.GetByTestId("nav-received-applications").IsVisibleAsync());
        Assert.Equal(0, await Page.GetByTestId("nav-applications").CountAsync());
    }

    [Fact]
    public async Task Advertiser_MyJobs_ShouldList_SeededJobs()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/my-jobs");
        Assert.True(response!.Ok, $"MyJobs retornou {response.Status}");

        await WaitForBlazorAsync();
        var jobRows = Page.GetByTestId("my-job-row");
        Assert.True(await jobRows.CountAsync() > 0,
            "Nenhuma vaga listada para o anunciante seedado");
    }

    [Fact]
    public async Task Advertiser_Can_CreateJob()
    {
        await LoginAsAdvertiserAsync();

        await GotoAsync("/jobs/create");
        await WaitForBlazorAsync();

        var jobTitle = $"Vaga E2E {DateTime.Now:HHmmss}";

        await Page.GetByTestId("job-title-input").FillAsync(jobTitle);
        await Page.GetByTestId("job-mode-remote").CheckAsync();
        await Page.GetByTestId("job-wizard-continue").ClickAsync();
        await Page.GetByTestId("job-description-input")
            .FillAsync("Vaga criada por teste E2E automatizado.");
        await Page.GetByTestId("job-wizard-continue").ClickAsync();
        await Page.GetByTestId("job-wizard-continue").ClickAsync();

        // A prévia mostra o cartão como na lista, mas a vaga ainda não existe: nada nele pode levar para fora do assistente
        var preview = Page.GetByTestId("job-wizard-preview");
        Assert.Contains(jobTitle, await preview.InnerTextAsync());
        Assert.Equal(0, await preview.Locator("a").CountAsync());

        var submit = Page.GetByTestId("job-create-submit");
        Assert.True(await submit.IsEnabledAsync(),
            "Botão de criação de vaga deveria estar habilitado");
        await submit.ClickAsync();
        // Publicada a vaga, a empresa abre a página dela já com o painel "Sua vaga"
        var detailTitle = Page.GetByTestId("job-detail-title");
        await detailTitle.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible
        });
        Assert.Equal(jobTitle, (await detailTitle.TextContentAsync())?.Trim());
        Assert.True(await Page.GetByTestId("job-owner-card").IsVisibleAsync(),
            "A empresa dona deveria ver o painel 'Sua vaga'");
    }

    [Fact]
    public async Task Advertiser_ReceivedApplications_ShouldLoad()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/received-applications");
        Assert.True(response!.Ok, $"ReceivedApplications retornou {response.Status}");
        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task Advertiser_Dashboard_ShouldLoad()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/dashboard");
        Assert.True(response!.Ok, $"Dashboard retornou {response.Status}");
        await WaitForBlazorAsync();

        var body = await Page.Locator("body").InnerTextAsync();
        Assert.Contains("Ver candidaturas recebidas", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Total de vagas", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUA JORNADA", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Advertiser_MySubscription_ShouldLoad()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/my-subscription");
        Assert.True(response!.Ok, $"MySubscription retornou {response.Status}");
        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task Advertiser_UploadLogo_PageShouldLoad()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/advertiser/upload-logo");
        Assert.True(response!.Ok, $"UploadLogo retornou {response.Status}");
        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task Address_Create_ShouldRenderForm()
    {
        await LoginAsAdvertiserAsync();

        var response = await GotoAsync("/address/create");
        Assert.True(response!.Ok, $"AddressCreate retornou {response.Status}");
        await WaitForBlazorAsync();
    }
}
