using FluentAssertions;

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
}
