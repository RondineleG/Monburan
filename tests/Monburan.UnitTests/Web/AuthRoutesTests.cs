using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AuthRoutesTests
{
    [Theory]
    [InlineData("/entrar")]
    [InlineData("/entrar/2fa")]
    [InlineData("/entrar?returnUrl=%2Fvagas")]
    [InlineData("/criar-conta")]
    [InlineData("/esqueci-senha/enviado")]
    [InlineData("/redefinir-senha?code=abc")]
    [InlineData("/confirmar-email?userId=1")]
    [InlineData("/reenviar-confirmacao")]
    [InlineData("/acesso-negado")]
    [InlineData("/conta-bloqueada")]
    [InlineData("/conta/usuario-invalido")]
    public void EntryPages_UseTheAuthLayout(string path)
    {
        AuthRoutes.IsAuthPage(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/conta")]
    [InlineData("/conta/seguranca-2fa")]
    [InlineData("/painel")]
    [InlineData("/vagas/nova")]
    [InlineData("/entrarei")]
    public void AccountAndAppPages_UseTheShell(string path)
    {
        AuthRoutes.IsAuthPage(path).Should().BeFalse();
    }
}
