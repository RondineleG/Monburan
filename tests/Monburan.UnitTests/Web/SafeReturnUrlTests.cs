using FluentAssertions;

using Monburan.Web.Components.Account;

namespace Monburan.UnitTests.Web;

public class SafeReturnUrlTests
{
    private const string Site = "https://monburan.com.br/";

    [Theory]
    [InlineData("/jobs?q=dev&local=sp", "/jobs?q=dev&local=sp")]
    [InlineData("jobs", "jobs")]
    [InlineData("https://monburan.com.br/Account/Manage", "Account/Manage")]
    [InlineData("HTTPS://MONBURAN.COM.BR/jobs", "jobs")]
    public void PathsOfThisSite_AreKept(string returnUrl, string expected) =>
        SafeReturnUrl.ToSitePath(returnUrl, Site).Should().Be(expected);

    [Theory]
    [InlineData("//evil.example/entrar")]
    [InlineData("/\\evil.example/entrar")]
    [InlineData("\\\\evil.example/entrar")]
    [InlineData("https://evil.example/entrar")]
    [InlineData("https://monburan.com.br.evil.example/entrar")]
    [InlineData("https://monburan.com.br///evil.example/entrar")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/\t/evil.example")]
    public void AnythingPointingElsewhere_GoesToTheHomePage(string returnUrl) =>
        SafeReturnUrl.ToSitePath(returnUrl, Site).Should().BeEmpty();

    [Fact]
    public void WithoutReturnUrl_GoesToTheHomePage() =>
        SafeReturnUrl.ToSitePath(null, Site).Should().BeEmpty();
}
