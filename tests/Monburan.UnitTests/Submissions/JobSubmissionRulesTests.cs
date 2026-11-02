using FluentAssertions;
using Monburan.Core.Enums;
using Monburan.Core.Services;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class JobSubmissionRulesTests
{
    [Theory]
    [InlineData(ESubmissionStatus.Pending, ESubmissionStatus.Approved, true)]
    [InlineData(ESubmissionStatus.Pending, ESubmissionStatus.Rejected, true)]
    [InlineData(ESubmissionStatus.Pending, ESubmissionStatus.Duplicate, true)]
    [InlineData(ESubmissionStatus.Pending, ESubmissionStatus.NeedsMoreInformation, true)]
    [InlineData(ESubmissionStatus.NeedsMoreInformation, ESubmissionStatus.Pending, true)]
    [InlineData(ESubmissionStatus.NeedsMoreInformation, ESubmissionStatus.Approved, true)]
    [InlineData(ESubmissionStatus.Approved, ESubmissionStatus.Rejected, false)]
    [InlineData(ESubmissionStatus.Rejected, ESubmissionStatus.Pending, false)]
    [InlineData(ESubmissionStatus.Duplicate, ESubmissionStatus.Approved, false)]
    [InlineData(ESubmissionStatus.Pending, ESubmissionStatus.Pending, false)]
    public void CanTransition_OnlyAllowsDecisionsOnOpenSubmissions(ESubmissionStatus from, ESubmissionStatus to, bool allowed)
    {
        JobSubmissionRules.CanTransition(from, to).Should().Be(allowed);
    }

    [Theory]
    [InlineData(ESubmissionDecision.Approve, ESubmissionStatus.Approved)]
    [InlineData(ESubmissionDecision.Reject, ESubmissionStatus.Rejected)]
    [InlineData(ESubmissionDecision.Duplicate, ESubmissionStatus.Duplicate)]
    [InlineData(ESubmissionDecision.NeedsMoreInformation, ESubmissionStatus.NeedsMoreInformation)]
    public void StatusFor_MapsEachDecision(ESubmissionDecision decision, ESubmissionStatus status)
    {
        JobSubmissionRules.StatusFor(decision).Should().Be(status);
    }

    [Fact]
    public void HasImageSignature_AcceptsTheRealFormatOfEachAllowedType()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
        var webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

        JobSubmissionRules.HasImageSignature(jpeg, "image/jpeg").Should().BeTrue();
        JobSubmissionRules.HasImageSignature(png, "image/png").Should().BeTrue();
        JobSubmissionRules.HasImageSignature(webp, "image/webp").Should().BeTrue();
    }

    [Fact]
    public void HasImageSignature_RejectsContentThatDoesNotMatchTheDeclaredType()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
        var script = "<script>alert(1)</script>"u8.ToArray();

        JobSubmissionRules.HasImageSignature(png, "image/jpeg").Should().BeFalse("PNG declarado como JPEG");
        JobSubmissionRules.HasImageSignature(script, "image/png").Should().BeFalse("texto não é imagem");
        JobSubmissionRules.HasImageSignature(script, "text/html").Should().BeFalse("tipo não permitido");
    }

    [Theory]
    [InlineData("https://vagas.exemplo.com.br/1", true)]
    [InlineData("http://exemplo.com.br", true)]
    [InlineData("ftp://exemplo.com.br/arquivo", false)]
    [InlineData("site sem protocolo.com.br", false)]
    [InlineData(null, false)]
    public void IsHttpUrl_OnlyAcceptsWebAddresses(string? url, bool expected)
    {
        JobSubmissionRules.IsHttpUrl(url).Should().Be(expected);
    }
}
