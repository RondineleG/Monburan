using FluentAssertions;
using Microsoft.AspNetCore.Http;

using Monburan.Web.Middleware;

namespace Monburan.UnitTests.Web;

public class LegacyRouteRedirectsTests
{
    [Theory]
    [InlineData("/Account/Login", "/entrar")]
    [InlineData("/account/login", "/entrar")]
    [InlineData("/Account/ConfirmEmail", "/confirmar-email")]
    [InlineData("/Account/ResetPassword", "/redefinir-senha")]
    [InlineData("/Account/Manage/TwoFactorAuthentication", "/conta/seguranca-2fa")]
    [InlineData("/Account/Manage", "/conta")]
    [InlineData("/dashboard", "/painel")]
    [InlineData("/jobs/create", "/vagas/nova")]
    [InlineData("/admin/ai-providers", "/admin/provedores-ia")]
    [InlineData("/privacy", "/privacidade")]
    public void ExactOldRoutes_RedirectToThePortugueseRoute(string oldPath, string newPath)
    {
        LegacyRouteRedirects.TargetFor(oldPath).Should().Be(newPath);
    }

    [Theory]
    [InlineData("/jobs/edit/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11", "/vagas/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/editar")]
    [InlineData("/jobs/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/applications", "/vagas/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11/candidaturas")]
    [InlineData("/address/edit/7", "/enderecos/7/editar")]
    [InlineData("/address/delete/7", "/enderecos/7/excluir")]
    [InlineData("/resumes/abc/edit", "/curriculos/abc/editar")]
    [InlineData("/resumes/abc/wizard", "/curriculos/abc/assistente")]
    [InlineData("/resumes/abc/cv", "/curriculos/abc/cv")]
    [InlineData("/resumes/abc", "/curriculos/abc")]
    [InlineData("/companies/abc", "/empresas/abc")]
    [InlineData("/apply/abc", "/candidatar/abc")]
    public void ParameterizedOldRoutes_KeepTheParameter(string oldPath, string newPath)
    {
        LegacyRouteRedirects.TargetFor(oldPath).Should().Be(newPath);
    }

    [Theory]
    [InlineData("/entrar")]
    [InlineData("/jobs")]
    [InlineData("/jobs/2b1c6e0e-6a3a-4f2b-9a51-0b8f2d0b4c11")]
    [InlineData("/api/v1/jobs/create")]
    [InlineData("/api/v1/resumes/mine")]
    [InlineData("/Account/Logout")]
    [InlineData("/Account/Manage/LinkExternalLogin")]
    public void RoutesThatDidNotChange_AreNotRedirected(string path)
    {
        LegacyRouteRedirects.TargetFor(path).Should().BeNull();
    }

    [Theory]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/css/site.css")]
    [InlineData("/images/logo.png")]
    [InlineData("/favicon.ico")]
    [InlineData("/")]
    [InlineData("")]
    public void AssetsAndOtherPaths_AreNotRedirected(string path)
    {
        LegacyRouteRedirects.TargetFor(path).Should().BeNull();
    }

    [Theory]
    [InlineData("/RESUMES/abc/edit", "/curriculos/abc/editar")]
    [InlineData("/Jobs/Edit/9", "/vagas/9/editar")]
    public void FirstSegmentFilter_IsCaseInsensitive(string oldPath, string newPath)
    {
        LegacyRouteRedirects.TargetFor(oldPath).Should().Be(newPath);
    }

    [Fact]
    public async Task Middleware_EncodesTheDestination_SoADecodedQuestionMarkDoesNotBecomeAQueryString()
    {
        // Request.Path chega decodificado: /resumes/a%3Fb vira "/resumes/a?b"
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = new PathString("/resumes/a?b#c");
        context.Request.QueryString = new QueryString("?x=1");

        await new LegacyRouteRedirectMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status301MovedPermanently);
        context.Response.Headers.Location.ToString().Should().Be("/curriculos/a%3Fb%23c?x=1");
    }

    [Fact]
    public async Task Middleware_DoesNotRedirectPost()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = new PathString("/Account/Login");
        var called = false;

        await new LegacyRouteRedirectMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
