using FluentAssertions;
using Monburan.Core.Validations;
using Xunit;

namespace Monburan.UnitTests.Validations;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("111.111.111-11", false)] // Todos dígitos iguais
    [InlineData("123.456.789-00", false)] // CPF inválido
    [InlineData("", false)] // Vazio
    [InlineData("123", false)] // Menos de 11 dígitos
    [InlineData("123456789012", false)] // Mais de 11 dígitos
    [InlineData("abc.def.ghi-jk", false)] // Letras
    [InlineData("529.982.247-26", false)] // Último dígito incorreto
    public void IsValid_WithVariousInputs_ReturnsExpectedResult(string cpf, bool expected)
    {
        // Act
        var result = CpfValidator.IsValid(cpf);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsValid_WithValidCpfFormatted_ReturnsTrue()
    {
        // Arrange
        var cpf = "529.982.247-25";

        // Act
        var result = CpfValidator.IsValid(cpf);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithValidCpfUnformatted_ReturnsTrue()
    {
        // Arrange
        var cpf = "52998224725";

        // Act
        var result = CpfValidator.IsValid(cpf);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithAllSameDigits_ReturnsFalse()
    {
        // Arrange
        var cpf = "111.111.111-11";

        // Act
        var result = CpfValidator.IsValid(cpf);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithInvalidCpf_ReturnsFalse()
    {
        // Arrange
        var cpf = "123.456.789-00";

        // Act
        var result = CpfValidator.IsValid(cpf);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNullCpf_ReturnsFalse()
    {
        // Act
        var result = CpfValidator.IsValid(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithEmptyCpf_ReturnsFalse()
    {
        // Act
        var result = CpfValidator.IsValid(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithWhitespaceCpf_ReturnsFalse()
    {
        // Act
        var result = CpfValidator.IsValid("   ");

        // Assert
        result.Should().BeFalse();
    }
}
