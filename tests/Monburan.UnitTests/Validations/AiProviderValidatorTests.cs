using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Validations;

namespace Monburan.UnitTests.Validations;

public class AiProviderValidatorTests
{
    private static AiProviderRequest Valid() => new()
    {
        Name = "OpenRouter",
        Protocol = EAiProtocol.OpenAiCompatible,
        BaseUrl = "https://openrouter.ai/api/v1",
        Model = "deepseek/deepseek-chat",
        ApiKeyEnvVar = "AI_KEY_OPENROUTER",
        TimeoutSeconds = 90,
        MaxOutputTokens = 4000,
        Temperature = 0.4
    };

    private static List<(string Field, string Message)> Validate(Action<AiProviderRequest> change)
    {
        var request = Valid();
        change(request);
        return AiProviderValidator.Validate(request);
    }

    private static void ShouldFailOnly(List<(string Field, string Message)> errors, string field) =>
        errors.Select(error => error.Field).Should().Equal([field]);

    [Fact]
    public void ARegularRequest_HasNoErrors() => AiProviderValidator.Validate(Valid()).Should().BeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Nome\ncom quebra de linha")]
    public void Name_MustBeBetweenTwoAndEightyCharactersWithoutControlCharacters(string name) =>
        ShouldFailOnly(Validate(request => request.Name = name), nameof(AiProviderRequest.Name));

    [Fact]
    public void Name_HasAnUpperLimit()
    {
        Validate(request => request.Name = new string('x', AiProviderRules.NameMaxLength)).Should().BeEmpty();
        ShouldFailOnly(Validate(request => request.Name = new string('x', AiProviderRules.NameMaxLength + 1)), nameof(AiProviderRequest.Name));
    }

    [Fact]
    public void Protocol_MustBeOneOfTheKnownOnes() =>
        ShouldFailOnly(Validate(request => request.Protocol = (EAiProtocol)99), nameof(AiProviderRequest.Protocol));

    [Theory]
    [InlineData("")]
    [InlineData("api.exemplo.com/v1")]
    [InlineData("http://api.exemplo.com/v1")]
    [InlineData("https://usuario:senha@api.exemplo.com/v1")]
    [InlineData("https://api.exemplo.com/v1?key=1")]
    public void BaseUrl_MustBeASafeAbsoluteAddress(string url) =>
        ShouldFailOnly(Validate(request => request.BaseUrl = url), nameof(AiProviderRequest.BaseUrl));

    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("deepseek/deepseek-chat")]
    [InlineData("anthropic/claude-haiku-4.5")]
    [InlineData("llama3.1:8b")]
    [InlineData("mimo-v2.6-pro")]
    [InlineData("openai/gpt-4o:online")]
    [InlineData("modelo@v1+x")]
    public void Model_AcceptsTheNamesProvidersUse(string model) =>
        Validate(request => request.Model = model).Should().BeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("modelo com espaço")]
    [InlineData("modelo;rm -rf")]
    [InlineData("modelo\"quote")]
    public void Model_IsRequiredAndCannotCarryUnusualCharacters(string model) =>
        ShouldFailOnly(Validate(request => request.Model = model), nameof(AiProviderRequest.Model));

    [Fact]
    public void Model_HasAnUpperLimit() =>
        ShouldFailOnly(Validate(request => request.Model = new string('m', AiProviderRules.ModelMaxLength + 1)), nameof(AiProviderRequest.Model));

    [Theory]
    [InlineData("")]
    [InlineData("ai_key_openrouter")]
    [InlineData("AI_KEY_")]
    [InlineData("JWT_SIGNING_KEY")]
    [InlineData("ConnectionStrings__Default")]
    [InlineData("AI_KEY_COM ESPAÇO")]
    public void ApiKeyEnvVar_MustFollowTheAiKeyPattern_SoNoOtherSecretCanBeReferenced(string name)
    {
        var errors = Validate(request => request.ApiKeyEnvVar = name);

        ShouldFailOnly(errors, nameof(AiProviderRequest.ApiKeyEnvVar));
        errors.Single().Message.Should().Contain(AiProviderRules.EnvVarPrefix);
    }

    [Fact]
    public void ApiKeyEnvVar_IgnoresSpacesAroundTheName() =>
        Validate(request => request.ApiKeyEnvVar = "  AI_KEY_OPENROUTER ").Should().BeEmpty();

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    [InlineData(0)]
    [InlineData(-1)]
    public void TimeoutSeconds_MustBeWithinTheLimits(int seconds) =>
        ShouldFailOnly(Validate(request => request.TimeoutSeconds = seconds), nameof(AiProviderRequest.TimeoutSeconds));

    [Theory]
    [InlineData(5)]
    [InlineData(300)]
    public void TimeoutSeconds_AcceptsTheLimits(int seconds) =>
        Validate(request => request.TimeoutSeconds = seconds).Should().BeEmpty();

    [Theory]
    [InlineData(255)]
    [InlineData(32001)]
    public void MaxOutputTokens_MustBeWithinTheLimits(int tokens) =>
        ShouldFailOnly(Validate(request => request.MaxOutputTokens = tokens), nameof(AiProviderRequest.MaxOutputTokens));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(double.NaN)]
    public void Temperature_MustBeBetweenZeroAndTwo_WhenItIsSent(double temperature) =>
        ShouldFailOnly(Validate(request => request.Temperature = temperature), nameof(AiProviderRequest.Temperature));

    [Fact]
    public void Temperature_IsIgnored_WhenItIsNotSent() =>
        Validate(request =>
        {
            request.SendTemperature = false;
            request.Temperature = 99;
        }).Should().BeEmpty();

    [Fact]
    public void SeveralProblems_AreAllReportedAtOnce()
    {
        var errors = Validate(request =>
        {
            request.Name = "";
            request.BaseUrl = "";
            request.Model = "";
            request.ApiKeyEnvVar = "";
        });

        errors.Select(error => error.Field).Should().BeEquivalentTo(
            nameof(AiProviderRequest.Name), nameof(AiProviderRequest.BaseUrl), nameof(AiProviderRequest.Model), nameof(AiProviderRequest.ApiKeyEnvVar));
    }
}
