using FluentAssertions;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;

using Monburan.Web.Services;

using NSubstitute;

namespace Monburan.UnitTests.Web;

public class CookieSecuritySetupTests
{
    private static CookieSecuritySetup Setup(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new CookieSecuritySetup(environment);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_TheSessionCookiesAreAlwaysSecure(string environmentName)
    {
        var options = new CookieAuthenticationOptions();

        Setup(environmentName).PostConfigure(IdentityConstants.ApplicationScheme, options);

        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
        options.Cookie.HttpOnly.Should().BeTrue();
        options.Cookie.SameSite.Should().Be(SameSiteMode.Lax);
    }

    [Fact]
    public void InDevelopment_TheSecureFlagFollowsTheRequest_BecauseLocalIsHttp()
    {
        var options = new CookieAuthenticationOptions();

        Setup("Development").PostConfigure(IdentityConstants.ApplicationScheme, options);

        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.SameAsRequest);
        options.Cookie.HttpOnly.Should().BeTrue();
    }

    [Theory]
    [InlineData("Identity.Application")]
    [InlineData("Identity.External")]
    [InlineData("Identity.TwoFactorRememberMe")]
    [InlineData("Identity.TwoFactorUserId")]
    public void EveryIdentityCookieIsCovered(string scheme)
    {
        var options = new CookieAuthenticationOptions();

        Setup("Production").PostConfigure(scheme, options);

        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
    }

    [Fact]
    public void OtherSchemesAreLeftAlone()
    {
        var options = new CookieAuthenticationOptions();
        var before = options.Cookie.SecurePolicy;

        Setup("Production").PostConfigure("OutroEsquema", options);
        Setup("Production").PostConfigure(null, options);

        options.Cookie.SecurePolicy.Should().Be(before);
    }
}
