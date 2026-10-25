namespace Monburan.Core.Interfaces.Services;

/// <summary>Imagem enviada junto da mensagem (só para provedores marcados como multimodais).</summary>
/// <param name="MediaType">Tipo MIME da imagem (image/jpeg, image/png ou image/webp).</param>
/// <param name="Data">Conteúdo binário da imagem.</param>
public sealed record AiImage(string MediaType, byte[] Data);

/// <summary>Mensagem de uma conversa (<c>system</c>, <c>user</c> ou <c>assistant</c>), com imagens opcionais.</summary>
public sealed record AiMessage(string Role, string Content, IReadOnlyList<AiImage>? Images = null);

/// <param name="Messages">Conversa enviada ao modelo.</param>
/// <param name="JsonObject">Pede ao provedor que responda com um objeto JSON (quando ele aceita).</param>
public sealed record AiCompletionRequest(IReadOnlyList<AiMessage> Messages, bool JsonObject = false);

/// <summary>Texto gerado, sem o raciocínio interno do modelo, e o consumo informado pelo provedor.</summary>
public sealed record AiCompletion(string Content, string? Model, int? PromptTokens, int? CompletionTokens);

/// <summary>Tipos de falha que a aplicação sabe tratar; os detalhes do provedor ficam só no log.</summary>
public enum AiFailureKind
{
    NotConfigured,

    /// <summary>O endereço do provedor aponta para uma rede interna ou reservada e a conexão foi recusada.</summary>
    BlockedDestination,

    Timeout,
    RateLimited,
    Unauthorized,
    ProviderError,
    InvalidResponse,
    Truncated,

    /// <summary>A mensagem tem imagens, mas o provedor não foi marcado como multimodal.</summary>
    VisionNotSupported
}
