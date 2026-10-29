using System.Security.Claims;

using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Services;
using Xunit;

namespace Monburan.UnitTests.Services;

public class FeatureFlagRulesTests
{
    private static ClaimsPrincipal User(string? role = null, string? userType = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (userType is not null)
            claims.Add(new Claim("user_type", userType));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "teste"));
    }

    [Fact]
    public void ImplicitGroups_ComeFromRoleAndProfile()
    {
        AccessGroups.ImplicitOf(User(role: "Admin")).Should().BeEquivalentTo(["admin"]);
        AccessGroups.ImplicitOf(User(role: "Produto")).Should().BeEquivalentTo(["produto"]);
        AccessGroups.ImplicitOf(User(userType: "candidate")).Should().BeEquivalentTo(["candidato"]);
        AccessGroups.ImplicitOf(User(userType: "advertiser")).Should().BeEquivalentTo(["recrutador"]);
    }

    [Fact]
    public void ImplicitGroups_AreEmpty_ForVisitors()
    {
        AccessGroups.ImplicitOf(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeEmpty();
        AccessGroups.ImplicitOf(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("candidato-beta", true)]
    [InlineData("recrutador-beta2", true)]
    [InlineData("Beta", false)]
    [InlineData("beta_grupo", false)]
    [InlineData("-beta", false)]
    [InlineData("beta-", false)]
    [InlineData("", false)]
    public void GroupName_IsLowercaseWithHyphens(string name, bool valid) =>
        AccessGroups.IsValidName(name).Should().Be(valid);

    [Fact]
    public void Flag_IsEnabled_ForEveryone_OrForAnyOfItsGroups()
    {
        var beta = new FeatureFlag { Name = "nova-busca", Groups = "candidato-beta, admin" };

        FeatureFlagRules.IsEnabled(beta, new HashSet<string> { "candidato", "candidato-beta" }).Should().BeTrue();
        FeatureFlagRules.IsEnabled(beta, new HashSet<string> { "admin" }).Should().BeTrue();
        FeatureFlagRules.IsEnabled(beta, new HashSet<string> { "candidato" }).Should().BeFalse();
        FeatureFlagRules.IsEnabled(beta, new HashSet<string>()).Should().BeFalse();

        var everyone = new FeatureFlag { Name = "aberta", EnabledForEveryone = true };
        FeatureFlagRules.IsEnabled(everyone, new HashSet<string>()).Should().BeTrue();

        var off = new FeatureFlag { Name = "desligada" };
        FeatureFlagRules.IsEnabled(off, new HashSet<string> { "admin" }).Should().BeFalse();
    }
}
