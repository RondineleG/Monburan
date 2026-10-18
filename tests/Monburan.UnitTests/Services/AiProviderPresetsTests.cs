using FluentAssertions;

using Monburan.Core.Enums;
using Monburan.Core.Services;
using Monburan.Core.Validations;

namespace Monburan.UnitTests.Services;

public class AiProviderPresetsTests
{
    private static IEnumerable<AiProviderPreset> Ready => AiProviderPresets.All.Where(preset => preset.Key != "custom");

    [Fact]
    public void EveryPresetHasAUniqueKeyAndLabel()
    {
        AiProviderPresets.All.Select(preset => preset.Key.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
        AiProviderPresets.All.Select(preset => preset.Label.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheListOffersTheProvidersTheAdministratorAskedFor()
    {
        AiProviderPresets.All.Select(preset => preset.Key).Should().Contain(
            ["xiaomi", "deepseek", "anthropic", "openai", "openrouter", "custom"]);
    }

    [Fact]
    public void EveryReadyPreset_UsesAnAllowedEnvVarAndAValidHttpsAddress()
    {
        foreach (var preset in Ready)
        {
            AiProviderRules.IsValidEnvVarName(preset.ApiKeyEnvVar).Should().BeTrue(preset.Key);
            AiProviderRules.TryParseBaseUrl(preset.BaseUrl, out var uri, out var error).Should().BeTrue($"{preset.Key}: {error}");
            uri!.Scheme.Should().Be("https", preset.Key);
            preset.Note.Should().NotBeNullOrWhiteSpace(preset.Key);
        }
    }

    [Fact]
    public void TheCustomPreset_StartsBlank_ForTheAdministratorToFill()
    {
        var custom = AiProviderPresets.Find("custom")!;

        custom.BaseUrl.Should().BeEmpty();
        custom.Model.Should().BeEmpty();
        custom.Protocol.Should().Be(EAiProtocol.OpenAiCompatible);
        custom.ApiKeyEnvVar.Should().Be(AiProviderRules.EnvVarPrefix);
        custom.SeedOnFirstRun.Should().BeFalse();
    }

    [Fact]
    public void OnlyPresetsWithAModelAreSeeded_SinceAnEmptyModelFailsTheScreenRules()
    {
        var seeded = AiProviderPresets.All.Where(preset => preset.SeedOnFirstRun).ToList();

        seeded.Should().NotBeEmpty();
        seeded.Should().OnlyContain(preset => !string.IsNullOrWhiteSpace(preset.Model));
        AiProviderPresets.All.Where(preset => string.IsNullOrWhiteSpace(preset.Model)).Should().OnlyContain(preset => !preset.SeedOnFirstRun);
    }

    [Fact]
    public void ExactlyOnePresetIsTheDefaultOfTheFirstRun_AndItIsXiaomi()
    {
        var defaults = AiProviderPresets.All.Where(preset => preset.SeedAsDefault).ToList();

        defaults.Should().ContainSingle().Which.Key.Should().Be("xiaomi");
        defaults[0].SeedOnFirstRun.Should().BeTrue();
    }

    [Fact]
    public void XiaomiIsOfferedInBothProtocols_WithTheSameKeyVariable()
    {
        var openAi = AiProviderPresets.Find("xiaomi")!;
        var anthropic = AiProviderPresets.Find("xiaomi-anthropic")!;

        openAi.Protocol.Should().Be(EAiProtocol.OpenAiCompatible);
        anthropic.Protocol.Should().Be(EAiProtocol.Anthropic);
        anthropic.ApiKeyEnvVar.Should().Be(openAi.ApiKeyEnvVar);
        openAi.BaseUrl.Should().EndWith("/v1");
        anthropic.BaseUrl.Should().EndWith("/anthropic");
    }

    [Fact]
    public void OpenAi_AsksForMaxCompletionTokens_BecauseNewModelsRefuseMaxTokens() =>
        AiProviderPresets.Find("openai")!.UseMaxCompletionTokens.Should().BeTrue();

    [Theory]
    [InlineData("xiaomi")]
    [InlineData("XIAOMI")]
    [InlineData("OpenRouter")]
    public void Find_IgnoresCase(string key) => AiProviderPresets.Find(key).Should().NotBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inexistente")]
    public void Find_ReturnsNull_ForNothingOrAnUnknownKey(string? key) => AiProviderPresets.Find(key).Should().BeNull();
}
