using FluentAssertions;

using Monburan.Core.Validations;

namespace Monburan.UnitTests.Validations;

public class AiProviderRulesTests
{
    [Theory]
    [InlineData("AI_KEY_OPENROUTER")]
    [InlineData("AI_KEY_XIAOMI")]
    [InlineData("AI_KEY_DEEPSEEK_2")]
    [InlineData("AI_KEY_A")]
    public void IsValidEnvVarName_AcceptsTheAiKeyPattern(string name) =>
        AiProviderRules.IsValidEnvVarName(name).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AI_KEY_")]
    [InlineData("ai_key_x")]
    [InlineData("AI_KEY_x")]
    [InlineData("JWT_SIGNING_KEY")]
    [InlineData("Jwt__SigningKey")]
    [InlineData("XAI_KEY_A")]
    [InlineData("AI_KEY_A B")]
    [InlineData("AI_KEY_A-B")]
    [InlineData("AI_KEY_A:B")]
    [InlineData("AI_KEY_A\n")]
    [InlineData(" AI_KEY_A")]
    public void IsValidEnvVarName_RejectsAnythingOutsideThePattern_SoNoOtherSecretCanBeNamed(string? name) =>
        AiProviderRules.IsValidEnvVarName(name).Should().BeFalse();

    [Fact]
    public void IsValidEnvVarName_HasALengthLimit()
    {
        AiProviderRules.IsValidEnvVarName("AI_KEY_" + new string('A', AiProviderRules.EnvVarSuffixMaxLength)).Should().BeTrue();
        AiProviderRules.IsValidEnvVarName("AI_KEY_" + new string('A', AiProviderRules.EnvVarSuffixMaxLength + 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://openrouter.ai/api/v1/")]
    [InlineData("https://api.deepseek.com")]
    [InlineData("  https://api.exemplo.com/v1  ")]
    [InlineData("https://token-plan-sgp.xiaomimimo.com/anthropic")]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:8080/v1")]
    public void TryParseBaseUrl_AcceptsHttpsAndLoopbackHttp(string value)
    {
        AiProviderRules.TryParseBaseUrl(value, out var uri, out var error).Should().BeTrue(error);
        uri.Should().NotBeNull();
        error.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "Informe o endereço")]
    [InlineData("", "Informe o endereço")]
    [InlineData("   ", "Informe o endereço")]
    [InlineData("api.exemplo.com/v1", "endereço completo")]
    [InlineData("não é url", "endereço completo")]
    [InlineData("file:///etc/passwd", "endereço completo")]
    [InlineData("http://api.exemplo.com/v1", "https://")]
    [InlineData("http://localhost.evil.com/v1", "https://")]
    [InlineData("ftp://api.exemplo.com", "https://")]
    [InlineData("https://usuario:senha@api.exemplo.com/v1", "usuário")]
    [InlineData("https://api.exemplo.com/v1?api_key=abc", "parâmetros")]
    [InlineData("https://api.exemplo.com/v1#ancora", "parâmetros")]
    public void TryParseBaseUrl_RejectsUnsafeOrIncompleteAddresses(string? value, string expectedMessagePart)
    {
        AiProviderRules.TryParseBaseUrl(value, out var uri, out var error).Should().BeFalse();
        uri.Should().BeNull();
        error.Should().Contain(expectedMessagePart);
    }

    [Fact]
    public void TryParseBaseUrl_RejectsAnAddressLongerThanTheLimit()
    {
        var value = "https://api.exemplo.com/" + new string('a', AiProviderRules.BaseUrlMaxLength);

        AiProviderRules.TryParseBaseUrl(value, out _, out var error).Should().BeFalse();
        error.Should().Contain(AiProviderRules.BaseUrlMaxLength.ToString());
    }

    [Theory]
    [InlineData("https://api.exemplo.com/v1", "https://api.exemplo.com/v1")]
    [InlineData("https://api.exemplo.com/v1/", "https://api.exemplo.com/v1")]
    [InlineData("  https://api.exemplo.com/v1//  ", "https://api.exemplo.com/v1")]
    [InlineData(null, "")]
    public void NormalizeBaseUrl_TrimsSpacesAndTrailingSlashes(string? value, string expected) =>
        AiProviderRules.NormalizeBaseUrl(value).Should().Be(expected);
}
