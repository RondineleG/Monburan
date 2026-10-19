using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;

namespace Monburan.Core.Entities;

/// <summary>
/// Provedor de IA configurado pelo administrador. A chave de acesso <b>nunca</b> é guardada aqui: só o NOME da
/// variável de ambiente (<c>AI_KEY_*</c>) que a contém no servidor.
/// </summary>
public class AiProvider : BaseEntity
{
    /// <summary>Nome mostrado à pessoa como destino do conteúdo (ex.: "Xiaomi MiMo"). Único.</summary>
    public string Name { get; set; } = string.Empty;

    public EAiProtocol Protocol { get; set; } = EAiProtocol.OpenAiCompatible;

    /// <summary>Raiz da API (https). Em OpenAI-compatível acrescenta-se <c>/chat/completions</c>; em Anthropic, <c>/v1/messages</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>Nome da variável de ambiente com a chave (sempre <c>AI_KEY_</c> + MAIÚSCULAS). Nunca a chave.</summary>
    public string ApiKeyEnvVar { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>Usado pelos recursos de IA quando nada diz o contrário. Só um provedor ativo é o padrão.</summary>
    public bool IsDefault { get; set; }

    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>Limite da resposta. Em modelos de raciocínio o raciocínio também conta: precisa de folga.</summary>
    public int MaxOutputTokens { get; set; } = 4000;

    public double Temperature { get; set; } = 0.4;

    /// <summary>Alguns modelos de raciocínio recusam o parâmetro de temperatura.</summary>
    public bool SendTemperature { get; set; } = true;

    /// <summary>Pede <c>response_format: json_object</c> (só protocolo OpenAI-compatível).</summary>
    public bool UseJsonResponseFormat { get; set; } = true;

    /// <summary>Envia <c>max_completion_tokens</c> no lugar de <c>max_tokens</c> (modelos novos da OpenAI).</summary>
    public bool UseMaxCompletionTokens { get; set; }

    public void MarkUpdated(string updatedBy)
    {
        UpdatedBy = updatedBy;
        Update();
    }
}
