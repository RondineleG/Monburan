using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

/// <summary>Criação ou edição de um provedor de IA (tela do administrador).</summary>
public class AiProviderRequest
{
    /// <summary>Vazio para criar; preenchido para editar.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public EAiProtocol Protocol { get; set; } = EAiProtocol.OpenAiCompatible;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>Nome completo da variável de ambiente com a chave (<c>AI_KEY_...</c>). Nunca a chave.</summary>
    public string ApiKeyEnvVar { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Torna este o provedor padrão ao salvar.</summary>
    public bool MakeDefault { get; set; }

    public int TimeoutSeconds { get; set; } = 90;

    public int MaxOutputTokens { get; set; } = 4000;

    public double Temperature { get; set; } = 0.4;

    public bool SendTemperature { get; set; } = true;

    public bool UseJsonResponseFormat { get; set; } = true;

    public bool UseMaxCompletionTokens { get; set; }

    /// <summary>Quem está salvando; preenchido pelo servidor com a identidade autenticada, nunca pelo cliente.</summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>Ação sobre um provedor existente (tornar padrão, excluir, testar).</summary>
public class AiProviderActionRequest
{
    public Guid Id { get; set; }

    /// <summary>Quem está agindo; preenchido pelo servidor.</summary>
    public string UpdatedBy { get; set; } = string.Empty;
}
