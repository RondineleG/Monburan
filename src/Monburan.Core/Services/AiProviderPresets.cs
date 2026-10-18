using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>Modelo pronto de provedor: facilita o cadastro com o endereço, o protocolo e o nome da variável da chave.</summary>
/// <param name="Note">Observação mostrada ao administrador (ex.: o que preencher).</param>
/// <param name="SeedOnFirstRun">Cadastrado sozinho na primeira execução (os demais só aparecem na lista de modelos).</param>
/// <param name="SeedAsDefault">Dos cadastrados na primeira execução, o que nasce ativo e como padrão.</param>
public sealed record AiProviderPreset(
    string Key,
    string Label,
    EAiProtocol Protocol,
    string BaseUrl,
    string Model,
    string ApiKeyEnvVar,
    string Note,
    bool SeedOnFirstRun = false,
    bool SeedAsDefault = false,
    bool UseMaxCompletionTokens = false);

/// <summary>
/// Modelos de provedores conhecidos. São só ponto de partida: o endereço, o modelo e o nome da variável valem o que o
/// administrador salvar na tela; depois de cadastrado, o banco é o único lugar com esses valores.
/// </summary>
public static class AiProviderPresets
{
    public static IReadOnlyList<AiProviderPreset> All { get; } =
    [
        new AiProviderPreset(
            "xiaomi", "Xiaomi MiMo", EAiProtocol.OpenAiCompatible,
            "https://token-plan-sgp.xiaomimimo.com/v1", "mimo-v2.6-pro", "AI_KEY_XIAOMI",
            "Modelo de raciocínio: o limite de saída precisa de folga.",
            SeedOnFirstRun: true, SeedAsDefault: true),
        new AiProviderPreset(
            "xiaomi-anthropic", "Xiaomi MiMo (protocolo Anthropic)", EAiProtocol.Anthropic,
            "https://token-plan-sgp.xiaomimimo.com/anthropic", "mimo-v2.6-pro", "AI_KEY_XIAOMI",
            "Mesmo serviço e mesma chave, pelo protocolo da Anthropic.",
            SeedOnFirstRun: true),
        new AiProviderPreset(
            "deepseek", "DeepSeek", EAiProtocol.OpenAiCompatible,
            "https://api.deepseek.com", "deepseek-chat", "AI_KEY_DEEPSEEK",
            "Use deepseek-reasoner para o modelo de raciocínio.",
            SeedOnFirstRun: true),
        new AiProviderPreset(
            "anthropic", "Anthropic (Claude)", EAiProtocol.Anthropic,
            "https://api.anthropic.com", "claude-haiku-5-5", "AI_KEY_ANTHROPIC",
            "Troque o modelo (claude-sonnet-5-5, claude-opus-5-5...) conforme a qualidade e o custo desejados.",
            SeedOnFirstRun: true),
        new AiProviderPreset(
            "openai", "OpenAI", EAiProtocol.OpenAiCompatible,
            "https://api.openai.com/v1", string.Empty, "AI_KEY_OPENAI",
            "Informe o modelo. Modelos novos (série o e GPT-5) pedem a opção \"usar max_completion_tokens\" e podem recusar a temperatura.",
            UseMaxCompletionTokens: true),
        new AiProviderPreset(
            "openrouter", "OpenRouter", EAiProtocol.OpenAiCompatible,
            "https://openrouter.ai/api/v1", "deepseek/deepseek-chat", "AI_KEY_OPENROUTER",
            "Um só endereço para centenas de modelos: troque o modelo, no formato fabricante/modelo, como aparece no catálogo do OpenRouter.",
            SeedOnFirstRun: true),
        new AiProviderPreset(
            "custom", "Outro (compatível com OpenAI)", EAiProtocol.OpenAiCompatible,
            string.Empty, string.Empty, "AI_KEY_",
            "Qualquer serviço que aceite POST /chat/completions (Ollama, LM Studio, gateways, outros provedores).")
    ];

    public static AiProviderPreset? Find(string? key) =>
        All.FirstOrDefault(preset => string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase));
}
