namespace Monburan.Core.Enums;

/// <summary>Protocolo de conversa com o provedor de IA.</summary>
public enum EAiProtocol
{
    /// <summary>"Chat completions" da OpenAI (<c>POST {url}/chat/completions</c>): OpenAI, OpenRouter, DeepSeek, Xiaomi, Ollama e outros.</summary>
    OpenAiCompatible = 0,

    /// <summary>API de mensagens da Anthropic (<c>POST {url}/v1/messages</c>): Claude e serviços que a imitam.</summary>
    Anthropic = 1
}
