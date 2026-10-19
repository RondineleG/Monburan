using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AiOptionsTests
{
    [Fact]
    public void ByDefault_PrivateNetworksAreBlocked_SoAProviderCannotReachInternalServices() =>
        new AiOptions().AllowPrivateNetworks.Should().BeFalse();

    [Fact]
    public void TheDefaultsAreConservative()
    {
        var options = new AiOptions();

        options.MaxRequestsPerHour.Should().Be(5);
        options.CatalogRefreshSeconds.Should().Be(60);
        AiOptions.SectionName.Should().Be("Ai");
    }
}
