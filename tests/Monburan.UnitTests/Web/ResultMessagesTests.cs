using FluentAssertions;

using Monburan.Core.Requests;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class ResultMessagesTests
{
    [Fact]
    public void ValidationErrors_BecomeTheMessage_EvenWithEmptyResultMessage()
    {
        var result = RequestResult<string>.WithNoContent();
        result.ValidationResult.AddError("Título é obrigatório.", "Title");
        result.ValidationResult.AddError("Data de expiração deve ser futura.", "ExpirationDate");

        ResultMessages.ToUserMessage(result).Should().Be("Título é obrigatório. Data de expiração deve ser futura.");
        ResultMessages.FieldErrors(result).Should().ContainKeys("Title", "ExpirationDate");
    }

    [Fact]
    public void DomainError_ShowsTheDomainMessage()
    {
        var result = RequestResult<string>.WithDomainError("Vaga não está mais ativa.");

        ResultMessages.ToUserMessage(result).Should().Be("Vaga não está mais ativa.");
    }

    [Fact]
    public void Conflict_ShowsTheEntityWarningMessage()
    {
        var result = RequestResult<string>.EntityAlreadyExists("Application", "1/2", "Você já se candidatou a esta vaga.");

        ResultMessages.ToUserMessage(result).Should().Be("Você já se candidatou a esta vaga.");
    }

    [Fact]
    public void InternalError_NeverExposesTheExceptionMessage()
    {
        var result = RequestResult<string>.WithError(new InvalidOperationException("23505: duplicate key value violates unique constraint \"IX_Secret\""));

        var message = ResultMessages.ToUserMessage(result, "Não foi possível salvar.");

        message.Should().Be("Não foi possível salvar.");
        message.Should().NotContain("duplicate");
    }
}
