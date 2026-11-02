using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Monburan.ApiTests;

/// <summary>Quem pode abrir a tela de administração dos provedores de IA.</summary>
public class AdminAiProvidersAccessApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;

    public AdminAiProvidersAccessApiTests(MonburanApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TheAdminPage_RedirectsAnonymousVisitorsToTheLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/provedores-ia");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/entrar").And.Contain("ReturnUrl");
    }
}
