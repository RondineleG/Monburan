using System.Text.Json;
using FluentAssertions;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.UnitTests.Web;

public class AiVisionPayloadTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    [Fact]
    public void ImagesToAProviderWithoutVision_AreRefusedBeforeAnySend()
    {
        var request = ImageRequest();

        var act = () => AnthropicProtocol.BuildPayload(Provider(supportsVision: false), request);

        act.Should().Throw<AiClientException>().Where(e => e.Kind == AiFailureKind.VisionNotSupported);
    }

    [Fact]
    public void ImagesToAVisionProvider_AreSentAsBase64Blocks()
    {
        var payload = AnthropicProtocol.BuildPayload(Provider(supportsVision: true), ImageRequest());

        var json = JsonSerializer.Serialize(payload);
        json.Should().Contain("\"type\":\"image\"");
        json.Should().Contain("\"media_type\":\"image/png\"");
        json.Should().Contain(Convert.ToBase64String(Png));
    }

    [Fact]
    public void GatewayCompatibleVisionProvider_SendsImageAsLocalDataUriPart()
    {
        var provider = Provider(supportsVision: true) with
        {
            Protocol = EAiProtocol.OpenAiCompatible,
            BaseUrl = "https://gateway.exemplo.com/v1",
            Model = "simulated-chat"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(OpenAiProtocol.BuildPayload(provider, ImageRequest())));
        var payload = document.RootElement;

        OpenAiProtocol.Endpoint(provider.BaseUrl).Should().Be(new Uri("https://gateway.exemplo.com/v1/chat/completions"));
        payload.GetProperty("model").GetString().Should().Be("simulated-chat");
        var userMessage = payload.GetProperty("messages")[1];
        userMessage.GetProperty("role").GetString().Should().Be("user");
        var parts = userMessage.GetProperty("content");
        parts.GetArrayLength().Should().Be(2);
        parts[0].GetProperty("type").GetString().Should().Be("text");
        parts[0].GetProperty("text").GetString().Should().Be("Veja a imagem.");
        parts[1].GetProperty("type").GetString().Should().Be("image_url");
        parts[1].GetProperty("image_url").GetProperty("url").GetString()
            .Should().Be("data:image/png;base64," + Convert.ToBase64String(Png));
        payload.GetRawText().Should().NotContain(OpenAiProtocol.Endpoint(provider.BaseUrl).AbsoluteUri);
    }

    [Fact]
    public void TextOnlyRequest_DoesNotNeedVisionAndSendsNoImage()
    {
        var request = new AiCompletionRequest([new AiMessage("user", "Olá")]);

        var payload = AnthropicProtocol.BuildPayload(Provider(supportsVision: false), request);

        JsonSerializer.Serialize(payload).Should().NotContain("\"type\":\"image\"");
    }

    private static AiCompletionRequest ImageRequest() => new(
    [
        new AiMessage("system", "Extraia os campos."),
        new AiMessage("user", "Veja a imagem.", [new AiImage("image/png", Png)])
    ]);

    private static AiProviderSettings Provider(bool supportsVision) => new(
        Guid.NewGuid(), "Teste", EAiProtocol.Anthropic, "https://api.exemplo.com", "modelo-x", "AI_KEY_TESTE",
        IsEnabled: true, IsDefault: true, TimeoutSeconds: 30, MaxOutputTokens: 1000, Temperature: 0.2,
        SendTemperature: true, UseJsonResponseFormat: true, UseMaxCompletionTokens: false, SupportsVision: supportsVision);
}
