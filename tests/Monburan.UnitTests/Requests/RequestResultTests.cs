using FluentAssertions;

using Monburan.Core.Requests;

using Xunit;

namespace Monburan.UnitTests.Requests;

public class RequestResultTests
{
    [Fact]
    public void WithValidations_Generic_FillsTheValidationResult()
    {
        // A sobrecarga com array de RequestValidation precisa encher o ValidationResult como
        // a de IEnumerable: quem só lê o ValidationResult (a API e a tela de erro) não via nada
        var result = RequestResult<string>.WithValidations(
            new RequestValidation("Name", "Informe um nome válido."),
            new RequestValidation("Email", "Informe um e-mail válido."));

        result.Status.Should().Be(Core.Enums.EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().HaveCount(2);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "Name");
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "Email");
        result.Validations.Should().HaveCount(2);
    }

    [Fact]
    public void WithValidations_Generic_WithPropertyAndDescription_FillsTheValidationResult()
    {
        var result = RequestResult<string>.WithValidations("Name", "Informe um nome válido.");

        result.ValidationResult.Errors.Should().ContainSingle(error =>
            error.Field == "Name" && error.Message == "Informe um nome válido.");
    }

    [Fact]
    public void WithValidations_Generic_WithEnumerable_FillsTheValidationResult()
    {
        var validations = new List<RequestValidation>
        {
            new("Name", "Informe um nome válido.")
        };

        var result = RequestResult<string>.WithValidations(validations);

        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == "Name");
    }

    [Fact]
    public void WithValidations_NonGeneric_FillsTheValidationResult()
    {
        var result = RequestResult.WithValidations(
            new RequestValidation("Name", "Informe um nome válido."));

        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == "Name");
    }

    [Fact]
    public void ToString_JoinsTheGeneralAndEntityAndValidationErrors()
    {
        var result = RequestResult<string>.WithNoContent();
        result.AddError("erro geral");
        result.AddEntityError("Vaga", "vaga inválida");
        result.ValidationResult.AddError("Informe um nome válido.", "Name");

        var text = result.ToString();

        text.Should().Contain("erro geral");
        text.Should().Contain("Vaga: vaga inválida");
        text.Should().Contain("Name: Informe um nome válido.");
    }
}
