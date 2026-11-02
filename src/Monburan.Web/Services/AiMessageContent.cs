using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Monta o conteúdo das mensagens com imagens para cada protocolo. Sem imagens, a mensagem segue como texto puro, do
/// jeito que sempre foi enviada. Com imagens, o provedor precisa estar marcado como multimodal: caso contrário nada é
/// enviado.
/// </summary>
internal static class AiMessageContent
{
    /// <exception cref="AiClientException">A mensagem tem imagens e o provedor não é multimodal.</exception>
    public static void EnsureVisionIfNeeded(AiProviderSettings provider, AiCompletionRequest request)
    {
        if (provider.SupportsVision)
            return;

        if (request.Messages.Any(message => message.Images is { Count: > 0 }))
            throw new AiClientException(AiFailureKind.VisionNotSupported, "O provedor padrão não aceita imagens.");
    }

    /// <summary>Chat completions: texto e imagens como partes <c>image_url</c> em data URI.</summary>
    public static object OpenAi(AiMessage message)
    {
        if (message.Images is not { Count: > 0 } images)
            return new { role = message.Role, content = message.Content };

        var parts = new List<object> { new { type = "text", text = message.Content } };
        foreach (var image in images)
            parts.Add(new { type = "image_url", image_url = new { url = $"data:{image.MediaType};base64,{Convert.ToBase64String(image.Data)}" } });

        return new { role = message.Role, content = parts.ToArray() };
    }

    /// <summary>Mensagens da Anthropic: blocos de imagem em base64, seguidos do texto.</summary>
    public static object Anthropic(AiMessage message)
    {
        if (message.Images is not { Count: > 0 } images)
            return new { role = message.Role, content = message.Content };

        var blocks = new List<object>();
        foreach (var image in images)
            blocks.Add(new { type = "image", source = new { type = "base64", media_type = image.MediaType, data = Convert.ToBase64String(image.Data) } });
        blocks.Add(new { type = "text", text = message.Content });

        return new { role = message.Role, content = blocks.ToArray() };
    }
}
