using FluentAssertions;
using Monburan.Core.Services;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class AiJobDraftParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private static string Single(string name, string raw) => "{\"" + name + "\": {\"value\": \"" + raw + "\", \"confidence\": \"high\"}}";

    [Fact]
    public void ReadsEachFieldWithItsConfidence()
    {
        var draft = AiJobDraftParser.Parse("""
            {"title": {"value": "Motorista de carreta", "confidence": "high"},
             "companyName": {"value": "Transportes Exemplo", "confidence": "high"},
             "description": {"value": "Transporte de cargas em rodovias.", "confidence": "medium"},
             "channel": {"value": "whatsapp", "confidence": "high"},
             "channelDetail": {"value": "(47) 99999-8888", "confidence": "high"}}
            """, Today);

        draft.Should().NotBeNull();
        draft!.Fields[AiJobDraftParser.Title].Value.Should().Be("Motorista de carreta");
        draft.Fields[AiJobDraftParser.Title].Confidence.Should().Be("high");
        draft.Fields[AiJobDraftParser.Channel].Value.Should().Be("WhatsApp");
        draft.NeedsReview.Should().BeFalse();
    }

    [Fact]
    public void MissingEssentialField_LeavesItEmptyAndAsksForReview()
    {
        var draft = AiJobDraftParser.Parse("""
            {"title": {"value": "Inspetor de qualidade", "confidence": "high"},
             "description": {"value": "Inspeção de peças.", "confidence": "high"},
             "channel": {"value": "email", "confidence": "high"}}
            """, Today);

        draft!.Fields[AiJobDraftParser.CompanyName].Value.Should().BeNull("a imagem não mostra a empresa");
        draft.Fields[AiJobDraftParser.CompanyName].Confidence.Should().Be("none");
        draft.NeedsReview.Should().BeTrue();
    }

    [Fact]
    public void LowConfidenceValue_AsksForReviewEvenWhenPresent()
    {
        var draft = AiJobDraftParser.Parse("""
            {"title": {"value": "Eletricista", "confidence": "high"},
             "companyName": {"value": "Construtora Exemplo", "confidence": "low"},
             "description": {"value": "Instalações elétricas.", "confidence": "high"},
             "channel": {"value": "onSite", "confidence": "high"}}
            """, Today);

        draft!.NeedsReview.Should().BeTrue();
        draft.Fields[AiJobDraftParser.CompanyName].Value.Should().Be("Construtora Exemplo", "o valor é mantido para o moderador conferir");
    }

    [Theory]
    [InlineData("8000", "8000")]
    [InlineData("8000.50", "8000.50")]
    [InlineData("8000,50", "8000.50")]
    [InlineData("8.000", "8000")]
    [InlineData("8.000,50", "8000.50")]
    [InlineData("R$ 8.000", "8000")]
    public void Salary_ReadsBrazilianAndPlainNumbers(string raw, string expected)
    {
        var draft = AiJobDraftParser.Parse(Single("salaryMin", raw), Today);

        draft!.Fields[AiJobDraftParser.SalaryMin].Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-300")]
    public void Salary_InvalidOrZeroIsDropped(string raw)
    {
        var draft = AiJobDraftParser.Parse(Single("salaryMin", raw), Today);

        draft!.Fields[AiJobDraftParser.SalaryMin].Value.Should().BeNull();
    }

    [Fact]
    public void Salary_IsNotInventedWhenAbsent()
    {
        var draft = AiJobDraftParser.Parse("""{"title": {"value": "Auxiliar", "confidence": "high"}}""", Today);

        draft!.Fields[AiJobDraftParser.SalaryMin].Value.Should().BeNull();
        draft.Fields[AiJobDraftParser.SalaryMax].Value.Should().BeNull();
        draft.Fields[AiJobDraftParser.SalaryPeriod].Value.Should().BeNull();
    }

    [Theory]
    [InlineData("fax", null)]
    [InlineData("externalUrl", "ExternalUrl")]
    [InlineData("onSite", "OnSite")]
    public void Channel_OnlyAcceptsTheKnownChannels(string raw, string? expected)
    {
        var draft = AiJobDraftParser.Parse(Single("channel", raw), Today);

        draft!.Fields[AiJobDraftParser.Channel].Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-11-30", "2026-11-30")]
    [InlineData("2026-10-01", null)]
    [InlineData("30/11/2026", null)]
    public void Deadline_MustBeAFutureIsoDate(string raw, string? expected)
    {
        var draft = AiJobDraftParser.Parse(Single("deadline", raw), Today);

        draft!.Fields[AiJobDraftParser.Deadline].Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("7241", "7241")]
    [InlineData("7241-10", "7241-10")]
    [InlineData("72-41", null)]
    public void CboCode_MustHaveTheCboShape(string raw, string? expected)
    {
        var draft = AiJobDraftParser.Parse(Single("cboCode", raw), Today);

        draft!.Fields[AiJobDraftParser.CboCode].Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"texto\"")]
    public void ResponseThatIsNotAnObject_IsUnreadable(string content)
    {
        AiJobDraftParser.Parse(content, Today).Should().BeNull();
    }
}
