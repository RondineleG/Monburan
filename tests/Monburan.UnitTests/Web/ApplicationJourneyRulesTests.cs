using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class ApplicationJourneyRulesTests
{
    [Theory]
    [InlineData("Sent", true)]
    [InlineData("Viewed", true)]
    [InlineData("Selected", false)]
    [InlineData("Rejected", false)]
    [InlineData("Archived", false)]
    public void IsInProgress_CoversOnlySentAndViewed(string status, bool expected)
    {
        ApplicationJourneyRules.IsInProgress(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("Sent", ApplicationJourneyFilter.All, true)]
    [InlineData("Sent", ApplicationJourneyFilter.InProgress, true)]
    [InlineData("Viewed", ApplicationJourneyFilter.InProgress, true)]
    [InlineData("Selected", ApplicationJourneyFilter.InProgress, false)]
    [InlineData("Selected", ApplicationJourneyFilter.Selected, true)]
    [InlineData("Rejected", ApplicationJourneyFilter.Closed, true)]
    [InlineData("Archived", ApplicationJourneyFilter.Closed, true)]
    [InlineData("Sent", ApplicationJourneyFilter.Closed, false)]
    public void Matches_FiltersBySituation(string status, ApplicationJourneyFilter filter, bool expected)
    {
        ApplicationJourneyRules.Matches(status, filter).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, ApplicationJourneyFilter.All)]
    [InlineData("", ApplicationJourneyFilter.All)]
    [InlineData("qualquer-coisa", ApplicationJourneyFilter.All)]
    [InlineData("todas", ApplicationJourneyFilter.All)]
    [InlineData("andamento", ApplicationJourneyFilter.InProgress)]
    [InlineData("selecionadas", ApplicationJourneyFilter.Selected)]
    [InlineData("encerradas", ApplicationJourneyFilter.Closed)]
    public void Parse_ReadsTheQueryStringValue(string? value, ApplicationJourneyFilter expected)
    {
        ApplicationJourneyRules.Parse(value).Should().Be(expected);
    }
}
