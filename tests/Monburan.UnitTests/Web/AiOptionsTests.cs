using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AiOptionsTests
{
    private static AiOptions Configured() => new()
    {
        BaseUrl = "https://api.exemplo.com/v1",
        ApiKey = "chave",
        Model = "modelo"
    };

    [Fact]
    public void IsConfigured_NeedsUrlKeyAndModel()
    {
        Configured().IsConfigured.Should().BeTrue();

        var withoutKey = Configured();
        withoutKey.ApiKey = "  ";
        withoutKey.IsConfigured.Should().BeFalse();

        var withoutModel = Configured();
        withoutModel.Model = string.Empty;
        withoutModel.IsConfigured.Should().BeFalse();

        var withoutUrl = Configured();
        withoutUrl.BaseUrl = string.Empty;
        withoutUrl.IsConfigured.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://api.exemplo.com/v1", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("https://api.exemplo.com/v1/", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("  https://api.exemplo.com/v1//  ", "https://api.exemplo.com/v1/chat/completions")]
    [InlineData("https://gateway.exemplo.com/openai/v1", "https://gateway.exemplo.com/openai/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    [InlineData("http://127.0.0.1:8080/v1", "http://127.0.0.1:8080/v1/chat/completions")]
    public void ChatCompletionsUri_AppendsThePathToTheBaseUrl(string baseUrl, string expected)
    {
        var options = Configured();
        options.BaseUrl = baseUrl;

        options.ChatCompletionsUri.Should().Be(new Uri(expected));
    }

    [Theory]
    [InlineData("http://api.exemplo.com/v1")]
    [InlineData("ftp://api.exemplo.com/v1")]
    [InlineData("api.exemplo.com/v1")]
    [InlineData("não é url")]
    [InlineData("")]
    public void ChatCompletionsUri_RejectsUnsafeOrInvalidAddresses_SoTheKeyNeverTravelsInPlainText(string baseUrl)
    {
        var options = Configured();
        options.BaseUrl = baseUrl;

        options.ChatCompletionsUri.Should().BeNull();
        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void DisplayName_UsesTheProviderNameOrTheServerHost()
    {
        var named = Configured();
        named.ProviderName = "  Xiaomi MiMo ";
        named.DisplayName.Should().Be("Xiaomi MiMo");

        Configured().DisplayName.Should().Be("api.exemplo.com");
        new AiOptions().DisplayName.Should().Be("provedor de IA");
    }
}
