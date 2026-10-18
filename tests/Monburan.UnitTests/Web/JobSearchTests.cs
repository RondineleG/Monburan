using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class JobSearchTests
{
    [Theory]
    [InlineData("SP", "", "SP")]
    [InlineData("sp", "", "SP")]
    [InlineData("Campinas, SP", "Campinas", "SP")]
    [InlineData("Campinas - sp", "Campinas", "SP")]
    [InlineData("São Paulo/SP", "São Paulo", "SP")]
    [InlineData("Belo Horizonte , MG", "Belo Horizonte", "MG")]
    [InlineData("Rio Claro", "Rio Claro", "")]
    [InlineData("Embu-Guaçu", "Embu-Guaçu", "")]
    [InlineData("Rio Claro - XX", "Rio Claro - XX", "")]
    [InlineData("  ", "", "")]
    public void ParseLocation_SplitsCityAndState(string input, string city, string state)
    {
        JobSearch.ParseLocation(input, out var parsedCity, out var parsedState);

        parsedCity.Should().Be(city);
        parsedState.Should().Be(state);
    }

    [Fact]
    public void ParseLocation_AcceptsNull()
    {
        JobSearch.ParseLocation(null, out var city, out var state);

        city.Should().BeEmpty();
        state.Should().BeEmpty();
    }

    [Theory]
    [InlineData("remoto", true)]
    [InlineData(" Remoto ", true)]
    [InlineData("Home Office", true)]
    [InlineData("remote", true)]
    [InlineData("Remotópolis", false)]
    [InlineData("São Paulo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRemoteKeyword_RecognizesRemoteWork(string? text, bool expected)
    {
        JobSearch.IsRemoteKeyword(text).Should().Be(expected);
    }
}
