using FluentAssertions;

using Microsoft.AspNetCore.Http;

using Monburan.Web.Middleware;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> RunAsync(string host = "monburan.com.br", bool https = true, bool reportOnly = false)
    {
        var options = new SecurityOptions { CspReportOnly = reportOnly };
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, new StaticOptionsMonitor<SecurityOptions>(options));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Scheme = https ? "https" : "http";

        await middleware.InvokeAsync(context);
        return context.Response.Headers;
    }

    private static string Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries).First(part => part == name || part.StartsWith(name + " ", StringComparison.Ordinal));

    [Fact]
    public async Task Scripts_AreOnlyFromTheSiteItself_WithNoInlineAndNoEval()
    {
        var headers = await RunAsync();
        var policy = headers["Content-Security-Policy"].ToString();

        Directive(policy, "script-src").Should().Be("script-src 'self'");
        policy.Should().NotContain("unsafe-eval");
        Directive(policy, "default-src").Should().Be("default-src 'self'");
    }

    [Fact]
    public async Task InlineStylesStayAllowedOnlyAsAttributes_NotAsStyleElements()
    {
        var policy = (await RunAsync())["Content-Security-Policy"].ToString();

        Directive(policy, "style-src-attr").Should().Be("style-src-attr 'unsafe-inline'");
        Directive(policy, "style-src-elem").Should().NotContain("unsafe-inline");
        Directive(policy, "style-src-elem").Should().Contain("https://fonts.googleapis.com");
    }

    [Fact]
    public async Task ClosesTheUsualHoles_Plugins_BaseTag_ExternalForms_Frames()
    {
        var policy = (await RunAsync())["Content-Security-Policy"].ToString();

        Directive(policy, "object-src").Should().Be("object-src 'none'");
        Directive(policy, "base-uri").Should().Be("base-uri 'self'");
        Directive(policy, "form-action").Should().Be("form-action 'self'");
        Directive(policy, "frame-ancestors").Should().Be("frame-ancestors 'none'");
    }

    [Fact]
    public async Task UpgradesInsecureRequests_OnlyBehindHttps()
    {
        (await RunAsync(https: true))["Content-Security-Policy"].ToString().Should().Contain("upgrade-insecure-requests");
        (await RunAsync(host: "localhost:5195", https: false))["Content-Security-Policy"].ToString().Should().NotContain("upgrade-insecure-requests");
    }

    [Theory]
    [InlineData("monburan.com.br", true, "wss://monburan.com.br")]
    [InlineData("localhost:5195", false, "ws://localhost:5195")]
    public async Task AllowsTheBlazorWebSocket_ToTheSameHostExplicitly(string host, bool https, string expected)
    {
        var policy = (await RunAsync(host, https))["Content-Security-Policy"].ToString();

        Directive(policy, "connect-src").Should().Be($"connect-src 'self' {expected}");
    }

    [Theory]
    [InlineData("evil.com; script-src *")]
    [InlineData("evil.com script-src")]
    [InlineData("a'b.com")]
    public async Task ADodgyHostHeader_NeverReachesThePolicy(string host)
    {
        var policy = (await RunAsync(host))["Content-Security-Policy"].ToString();

        Directive(policy, "connect-src").Should().Be("connect-src 'self'");
        policy.Should().NotContain("evil.com");
        Directive(policy, "script-src").Should().Be("script-src 'self'");
    }

    [Fact]
    public async Task ReportOnlyMode_SwapsTheHeaderSoNothingIsBlocked()
    {
        var headers = await RunAsync(reportOnly: true);

        headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
        headers["Content-Security-Policy-Report-Only"].ToString().Should().Contain("script-src 'self'");
    }

    [Fact]
    public async Task SendsTheOtherSecurityHeaders()
    {
        var headers = await RunAsync();

        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        headers["X-Frame-Options"].ToString().Should().Be("DENY");
        headers["X-XSS-Protection"].ToString().Should().Be("0");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
    }
}
