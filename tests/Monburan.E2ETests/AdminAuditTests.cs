using Microsoft.Playwright;

namespace Monburan.E2ETests;

/// <summary>Trilha de auditoria: o que o administrador faz fica registrado e só administradores veem.</summary>
public class AdminAuditTests : E2ETestBase
{
    private const string AdminEmail = "admin@monburan.com.br";
    private const string AuditPath = "/admin/audit";

    public AdminAuditTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    private async Task OpenAuditAsync()
    {
        await GotoAsync(AuditPath);
        await WaitForBlazorAsync();
        await Page.GetByTestId("audit-search").WaitForAsync();
    }

    private async Task SearchAsync(string term)
    {
        await Page.GetByTestId("audit-search").FillAsync(term);
        await Page.GetByTestId("audit-apply").ClickAsync();
    }

    [Fact]
    public async Task ChangesToAiProviders_AppearInTheAuditTrail_WithWhoDidWhat()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        var name = "E2E Auditoria " + Guid.NewGuid().ToString("N")[..8];

        // Cadastra e exclui um provedor pela tela (nenhuma chamada de rede: só o cadastro)
        await GotoAsync("/admin/ai-providers");
        await WaitForBlazorAsync();
        await Page.GetByTestId("ai-status").WaitForAsync();
        await Page.GetByTestId("ai-provider-new").ClickAsync();
        await Page.GetByTestId("ai-form-name").FillAsync(name);
        await Page.GetByTestId("ai-form-url").FillAsync("https://api.exemplo.com/v1");
        await Page.GetByTestId("ai-form-model").FillAsync("modelo-e2e");
        await Page.GetByTestId("ai-form-env").FillAsync("e2e_auditoria");
        await Page.GetByTestId("ai-form-save").ClickAsync();
        var card = Page.Locator($"[data-testid='ai-provider-card'][data-provider-name='{name}']");
        await card.WaitForAsync();
        await card.GetByTestId("ai-provider-delete").ClickAsync();
        await Page.GetByTestId("confirm-accept").ClickAsync();
        await card.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        await OpenAuditAsync();
        await SearchAsync(name);

        var rows = Page.GetByTestId("audit-row");
        await Assertions.Expect(rows).ToHaveCountAsync(2);
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Provedor de IA excluído" })).ToHaveCountAsync(1);
        await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "Provedor de IA salvo" })).ToHaveCountAsync(1);
        await Assertions.Expect(rows.First).ToContainTextAsync(AdminEmail);
        await Assertions.Expect(rows.First).ToContainTextAsync(name);
    }

    [Fact]
    public async Task TheFilters_NarrowTheList()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await OpenAuditAsync();

        await Page.GetByTestId("audit-action").SelectOptionAsync("ai_provider.deleted");
        await Page.GetByTestId("audit-apply").ClickAsync();

        // Espera a lista filtrada chegar: não pode sobrar nenhuma linha de outra ação
        await Assertions.Expect(Page.Locator("[data-testid='audit-row']:not([data-action='ai_provider.deleted'])")).ToHaveCountAsync(0);

        await SearchAsync("termo-que-nao-existe-" + Guid.NewGuid().ToString("N"));
        await Assertions.Expect(Page.GetByTestId("audit-empty")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_SeesTheMenuLink()
    {
        Assert.True(await TryLoginAsync(AdminEmail, TestPassword));
        await GotoAsync("/");
        await WaitForBlazorAsync();

        await Assertions.Expect(Page.GetByTestId("nav-admin-audit")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Candidate_CannotOpenTheAuditTrail()
    {
        Assert.True(await TryLoginAsync(CandidateEmail, TestPassword));
        await GotoAsync("/");
        await WaitForBlazorAsync();
        Assert.Equal(0, await Page.GetByTestId("nav-admin-audit").CountAsync());

        await GotoAsync(AuditPath);

        Assert.Contains("AccessDenied", Page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Page.GetByTestId("audit-row").CountAsync());
    }

    [Fact]
    public async Task Anonymous_IsSentToTheLogin()
    {
        await GotoAsync(AuditPath);

        Assert.Contains("/Account/Login", Page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
