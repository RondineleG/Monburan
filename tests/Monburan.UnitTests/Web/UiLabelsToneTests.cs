using System.Security.Claims;

using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class UiLabelsToneTests
{
    [Theory]
    [InlineData("Sent", StatusTone.Neutral)]
    [InlineData("Viewed", StatusTone.Info)]
    [InlineData("Selected", StatusTone.Ok)]
    [InlineData("Rejected", StatusTone.Error)]
    [InlineData("Archived", StatusTone.Neutral)]
    [InlineData("Interview", StatusTone.Brand)]
    public void ApplicationStatus_HasTheToneOfTheDesignSystem(string status, StatusTone tone) =>
        UiLabels.ApplicationStatusTone(status).Should().Be(tone);

    [Theory]
    [InlineData("Active", StatusTone.Ok)]
    [InlineData("Paused", StatusTone.Neutral)]
    [InlineData("Expired", StatusTone.Neutral)]
    [InlineData("Hidden", StatusTone.Warn)]
    [InlineData("Rejected", StatusTone.Error)]
    public void JobStatus_HasTheToneOfTheDesignSystem(string status, StatusTone tone) =>
        UiLabels.JobStatusTone(status).Should().Be(tone);

    [Fact]
    public void EveryTone_MapsToABadgeClass() =>
        Enum.GetValues<StatusTone>().Select(UiLabels.ToneClass).Should().OnlyHaveUniqueItems().And.AllSatisfy(c => c.Should().StartWith("badge--"));

    [Theory]
    [InlineData("João Silva", "JS")]
    [InlineData("Maria da Conceição Santos", "MS")]
    [InlineData("Tech Solutions Ltda", "TL")]
    [InlineData("ana", "AN")]
    [InlineData("  ", "?")]
    public void Initials_TakeFirstAndLastWord(string name, string expected) =>
        ClaimsExtensions.Initials(name).Should().Be(expected);

    [Fact]
    public void DisplayName_FallsBackToTheEmailLocalPart_WhenThereIsNoProfile()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@monburan.com.br")], "test"));

        principal.GetDisplayName().Should().Be("admin");
        principal.GetInitials().Should().Be("AD");
    }

    [Fact]
    public void DisplayName_ComesFromTheProfileClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "joao.silva@email.com"), new Claim(ClaimsExtensions.DisplayNameClaimType, "João Silva")], "test"));

        principal.GetDisplayName().Should().Be("João Silva");
        principal.GetInitials().Should().Be("JS");
    }
}
