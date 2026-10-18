using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Web.Models;

namespace Monburan.ApiTests;

/// <summary>A mesma API com <c>Admin:Emails</c> definido: quem pode virar administrador sem estar no banco.</summary>
public class AdminEmailsApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Emails"] = " dono@exemplo.com ; Outra.Pessoa@Exemplo.com,admin@monburan.com.br;nao.confirmou@exemplo.com"
            }));
    }
}

public class AdminBootstrapApiTests : IClassFixture<AdminEmailsApiFactory>, IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";

    private readonly AdminEmailsApiFactory _configured;
    private readonly MonburanApiFactory _plain;

    public AdminBootstrapApiTests(AdminEmailsApiFactory configured, MonburanApiFactory plain)
    {
        _configured = configured;
        _plain = plain;
    }

    private static async Task<ClaimsPrincipal> PrincipalAsync(WebApplicationFactory<Program> factory, string email, bool emailConfirmed)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = emailConfirmed, CreatedAt = DateTime.UtcNow, IsActive = true };
            (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        }

        return await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
    }

    [Theory]
    [InlineData("dono@exemplo.com")]
    [InlineData("DONO@EXEMPLO.COM")]
    [InlineData("outra.pessoa@exemplo.com")]
    public async Task AConfirmedEmailInTheList_BecomesAdministrator_IgnoringCase(string email)
    {
        var principal = await PrincipalAsync(_configured, email, emailConfirmed: true);

        principal.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public async Task AnUnconfirmedEmailInTheList_IsNotAdministrator_SoNobodyCanClaimAnotherPersonsEmail()
    {
        var principal = await PrincipalAsync(_configured, "nao.confirmou@exemplo.com", emailConfirmed: false);

        principal.IsInRole("Admin").Should().BeFalse();
    }

    [Fact]
    public async Task AConfirmedEmailOutsideTheList_IsNotAdministrator()
    {
        var principal = await PrincipalAsync(_configured, "qualquer.pessoa@exemplo.com", emailConfirmed: true);

        principal.IsInRole("Admin").Should().BeFalse();
    }

    [Fact]
    public async Task WithoutTheSetting_NobodyBecomesAdministratorByEmail()
    {
        var principal = await PrincipalAsync(_plain, "dono@exemplo.com", emailConfirmed: true);

        principal.IsInRole("Admin").Should().BeFalse();
    }

    [Fact]
    public async Task AnAdministratorAlreadyInTheDatabase_GetsTheRoleOnlyOnce_EvenWhenAlsoInTheList()
    {
        var principal = await PrincipalAsync(_configured, "admin@monburan.com.br", emailConfirmed: true);

        principal.FindAll(ClaimTypes.Role).Count(claim => claim.Value == "Admin").Should().Be(1);
    }

    [Fact]
    public async Task TheSeededAdministrator_StillWorksWithoutTheSetting()
    {
        var principal = await PrincipalAsync(_plain, "admin@monburan.com.br", emailConfirmed: true);

        principal.IsInRole("Admin").Should().BeTrue();
    }
}
