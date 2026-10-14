using FluentAssertions;
using Monburan.Core.Validations;
using Xunit;

namespace Monburan.UnitTests.Validations;

public class CnpjValidatorTests
{
    [Theory]
    [InlineData("11.444.777/0001-61", true)]
    [InlineData("11444777000161", true)]
    [InlineData("11.111.111/1111-11", false)] // Todos dígitos iguais
    [InlineData("12.345.678/0001-00", false)] // CNPJ inválido
    [InlineData("", false)] // Vazio
    [InlineData("123", false)] // Menos de 14 dígitos
    [InlineData("123456789012345", false)] // Mais de 14 dígitos
    [InlineData("abc.def.ghi/jkl-mn", false)] // Letras
    [InlineData("11.444.777/0001-62", false)] // Último dígito incorreto
    public void IsValid_WithVariousInputs_ReturnsExpectedResult(string cnpj, bool expected)
    {
        // Act
        var result = CnpjValidator.IsValid(cnpj);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsValid_WithValidCnpjFormatted_ReturnsTrue()
    {
        // Arrange
        var cnpj = "11.444.777/0001-61";

        // Act
        var result = CnpjValidator.IsValid(cnpj);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithValidCnpjUnformatted_ReturnsTrue()
    {
        // Arrange
        var cnpj = "11444777000161";

        // Act
        var result = CnpjValidator.IsValid(cnpj);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithAllSameDigits_ReturnsFalse()
    {
        // Arrange
        var cnpj = "11.111.111/1111-11";

        // Act
        var result = CnpjValidator.IsValid(cnpj);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithInvalidCnpj_ReturnsFalse()
    {
        // Arrange
        var cnpj = "12.345.678/0001-00";

        // Act
        var result = CnpjValidator.IsValid(cnpj);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNullCnpj_ReturnsFalse()
    {
        // Act
        var result = CnpjValidator.IsValid(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyCnpj_ReturnsFalse()
    {
        // Act
        var result = CnpjValidator.IsValid(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithWhitespaceCnpj_ReturnsFalse()
    {
        // Act
        var result = CnpjValidator.IsValid("   ");

        // Assert
        result.Should().BeFalse();
    }
}
