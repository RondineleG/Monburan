using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.UnitTests.Web;

public class HmacSubmitterKeyHasherTests
{
    private static HmacSubmitterKeyHasher Create(string? key) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Submissions:HashKey"] = key }).Build(),
        NullLogger<HmacSubmitterKeyHasher>.Instance);

    [Fact]
    public void SameKeyAndValue_GiveTheSameHash()
    {
        Create("segredo").Hash("203.0.113.7").Should().Be(Create("segredo").Hash("203.0.113.7"));
    }

    [Fact]
    public void DifferentKeys_GiveDifferentHashes_SoThePlainSha256CannotBeRecomputed()
    {
        Create("segredo-1").Hash("203.0.113.7").Should().NotBe(Create("segredo-2").Hash("203.0.113.7"));
    }

    [Fact]
    public void HashFitsTheColumn_AndDoesNotLeakTheAddress()
    {
        var hash = Create("segredo").Hash("203.0.113.7");

        hash.Should().HaveLength(64).And.NotContain("203.0.113.7");
    }

    [Fact]
    public void WithoutAConfiguredKey_StillHashes_WithAPerProcessRandomKey()
    {
        var first = Create(null);
        var second = Create(null);

        first.Hash("x").Should().Be(first.Hash("x"));
        first.Hash("x").Should().NotBe(second.Hash("x"));
    }
}
