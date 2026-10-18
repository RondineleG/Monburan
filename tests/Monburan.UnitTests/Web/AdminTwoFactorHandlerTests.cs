using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Authorization;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AdminTwoFactorHandlerTests
{
    private static async Task<bool> SucceedsAsync(bool required, params Claim[] claims)
    {
        var handler = new AdminTwoFactorHandler(new StaticOptionsMonitor<SecurityOptions>(new SecurityOptions { RequireTwoFactorForAdmins = required }));
        var requirement = new AdminTwoFactorRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "teste"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task WhenRequired_AnAdminWithoutTwoFactorIsRefused() =>
        (await SucceedsAsync(true, new Claim(ClaimTypes.Role, "Admin"))).Should().BeFalse();

    [Fact]
    public async Task WhenRequired_AnAdminWithTwoFactorIsAccepted() =>
        (await SucceedsAsync(true, new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "true"))).Should().BeTrue();

    [Fact]
    public async Task WhenRequired_AClaimWithAnotherValueDoesNotCount() =>
        (await SucceedsAsync(true, new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "false"))).Should().BeFalse();

    [Fact]
    public async Task WhenNotRequired_EveryoneIsAccepted_ThisIsHowDevelopmentRuns() =>
        (await SucceedsAsync(false, new Claim(ClaimTypes.Role, "Admin"))).Should().BeTrue();

    [Fact]
    public void HasTwoFactor_ReadsTheClaim()
    {
        var with = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "true")], "teste"));
        var without = new ClaimsPrincipal(new ClaimsIdentity([], "teste"));

        AdminTwoFactorHandler.HasTwoFactor(with).Should().BeTrue();
        AdminTwoFactorHandler.HasTwoFactor(without).Should().BeFalse();
    }
}
