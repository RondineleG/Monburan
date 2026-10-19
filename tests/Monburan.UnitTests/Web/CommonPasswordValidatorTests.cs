using FluentAssertions;

using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class CommonPasswordValidatorTests
{
    private readonly CommonPasswordValidator _validator = new();

    private static ApplicationUser User(string email = "joao.silva@exemplo.com") => new() { Email = email, UserName = email };

    private async Task<(bool Succeeded, string[] Codes)> ValidateAsync(string? password, ApplicationUser? user = null)
    {
        // O validador não usa o UserManager: só olha a senha e a conta
        var result = await _validator.ValidateAsync(null!, user ?? User(), password);
        return (result.Succeeded, result.Errors.Select(error => error.Code).ToArray());
    }

    [Theory]
    [InlineData("Senha12345")]
    [InlineData("senha12345")]
    [InlineData("Senha@12345")]
    [InlineData("Password123")]
    [InlineData("Passw0rd123")]
    [InlineData("Qwerty12345")]
    [InlineData("Mudar@12345")]
    [InlineData("Brasil2026")]
    [InlineData("1Q2W3E4R5T")]
    public async Task RejectsTheMostCommonPasswords_IgnoringCaseAndSymbols(string password)
    {
        var (succeeded, codes) = await ValidateAsync(password);

        succeeded.Should().BeFalse();
        codes.Should().Equal("PasswordTooCommon");
    }

    [Theory]
    [InlineData("joao.silva@exemplo.com")]
    [InlineData("Joao.Silva@Exemplo.com")]
    [InlineData("joaosilva123")]
    [InlineData("Joao.Silva2026")]
    public async Task RejectsThePasswordWhenItIsTheAccountEmailOrItsBeginningPlusNumbers(string password)
    {
        var (succeeded, codes) = await ValidateAsync(password);

        succeeded.Should().BeFalse();
        codes.Should().Equal("PasswordSameAsAccount");
    }

    [Theory]
    [InlineData("aaaaaaaaaa")]
    [InlineData("AaAaAaAaAa1")]
    [InlineData("1234567890")]
    [InlineData("0987654321")]
    [InlineData("abcdefghijk")]
    public async Task RejectsRepeatedCharactersAndObviousSequences(string password)
    {
        var (succeeded, codes) = await ValidateAsync(password);

        succeeded.Should().BeFalse();
        codes.Should().Contain("PasswordTooSimple");
    }

    [Theory]
    [InlineData("Cavalo-Azul-Bateria-7")]
    [InlineData("Limite#Plano2026")]
    [InlineData("Monburan123")]
    [InlineData("T3ste.de.Senha.Longa")]
    public async Task AcceptsOrdinaryPasswords(string password)
    {
        var (succeeded, _) = await ValidateAsync(password);

        succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LeavesEmptyPasswordsToTheOtherRules(string? password)
    {
        var (succeeded, _) = await ValidateAsync(password);

        succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotCompareWithAnAccountThatHasNoEmail()
    {
        var (succeeded, _) = await ValidateAsync("Cavalo-Azul-Bateria-7", new ApplicationUser());

        succeeded.Should().BeTrue();
    }
}
