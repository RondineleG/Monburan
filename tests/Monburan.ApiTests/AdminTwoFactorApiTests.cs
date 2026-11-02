using System.Net;
using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>Como em produção: administradores só entram na administração com a verificação em duas etapas ligada.</summary>
public class TwoFactorRequiredApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:RequireTwoFactorForAdmins"] = "true" }));
    }
}

public class AdminTwoFactorApiTests : IClassFixture<TwoFactorRequiredApiFactory>, IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";

    private readonly TwoFactorRequiredApiFactory _required;
    private readonly MonburanApiFactory _development;

    public AdminTwoFactorApiTests(TwoFactorRequiredApiFactory required, MonburanApiFactory development)
    {
        _required = required;
        _development = development;
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "teste"));

    private static async Task<bool> AdminOnlyAsync(WebApplicationFactory<Program> factory, ClaimsPrincipal user)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, "AdminOnly");
        return result.Succeeded;
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [Fact]
    public async Task WhenRequired_TheAdminPolicy_NeedsTheRoleAndTheTwoFactorClaim()
    {
        var admin = new Claim(ClaimTypes.Role, "Admin");
        var twoFactor = new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "true");

        (await AdminOnlyAsync(_required, Principal(admin))).Should().BeFalse("administrador sem 2FA");
        (await AdminOnlyAsync(_required, Principal(admin, twoFactor))).Should().BeTrue();
        (await AdminOnlyAsync(_required, Principal(twoFactor))).Should().BeFalse("2FA sem o papel de administrador não basta");
    }

    [Fact]
    public async Task InDevelopment_TheAdminPolicy_OnlyNeedsTheRole()
    {
        (await AdminOnlyAsync(_development, Principal(new Claim(ClaimTypes.Role, "Admin")))).Should().BeTrue();
        (await AdminOnlyAsync(_development, Principal())).Should().BeFalse();
    }

    [Fact]
    public async Task AnAdminWithoutTwoFactor_IsSentToTheSetupPage_NotToAccessDenied()
    {
        using var client = Client(_required);
        await CookieAuth.LoginAsync(client, "admin@monburan.com.br", Password);

        var response = await client.GetAsync("/admin/provedores-ia");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/conta/seguranca-2fa").And.Contain("adminRequired=true");
    }

    [Fact]
    public async Task TheSetupPage_ExplainsWhyTheAdminWasSentThere()
    {
        using var client = Client(_required);
        await CookieAuth.LoginAsync(client, "admin@monburan.com.br", Password);

        var html = await client.GetStringAsync("/conta/seguranca-2fa?adminRequired=true");

        html.Should().Contain("admin-2fa-required").And.Contain("Para usar as áreas de administração, ative a verificação em duas etapas");
    }

    [Fact]
    public async Task ACandidate_StillGetsAccessDenied_NotTheTwoFactorRedirect()
    {
        using var client = Client(_required);
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", Password);

        var response = await client.GetAsync("/admin/provedores-ia");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/acesso-negado");
    }

    [Fact]
    public async Task InDevelopment_TheAdminOpensTheAdminPageWithoutTwoFactor()
    {
        using var client = Client(_development);
        await CookieAuth.LoginAsync(client, "admin@monburan.com.br", Password);

        var response = await client.GetAsync("/admin/provedores-ia");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheSignInCarriesTheTwoFactorClaim_OnlyWhenTheAccountHasItOn()
    {
        using var scope = _required.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var claimsFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();

        var email = $"dois-fatores-{Guid.NewGuid():N}@teste.com";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, CreatedAt = DateTime.UtcNow, IsActive = true };
        (await users.CreateAsync(user, "Cavalo-Azul-Bateria-7")).Succeeded.Should().BeTrue();

        (await claimsFactory.CreateAsync(user)).HasClaim(ClaimsExtensions.TwoFactorEnabledClaimType, "true").Should().BeFalse();

        await users.SetTwoFactorEnabledAsync(user, true);

        (await claimsFactory.CreateAsync(user)).HasClaim(ClaimsExtensions.TwoFactorEnabledClaimType, "true").Should().BeTrue();
    }
}
