using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Response;

public class AiProviderResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public EAiProtocol Protocol { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ApiKeyEnvVar { get; set; } = string.Empty;

    /// <summary>A variável de ambiente existe e tem valor neste servidor (o valor nunca sai daqui).</summary>
    public bool KeyConfigured { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsDefault { get; set; }

    public int TimeoutSeconds { get; set; }

    public int MaxOutputTokens { get; set; }

    public double Temperature { get; set; }

    public bool SendTemperature { get; set; }

    public bool UseJsonResponseFormat { get; set; }

    public bool UseMaxCompletionTokens { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>Resultado do botão "Testar" de um provedor.</summary>
public class AiProviderTestResponse
{
    public bool Success { get; set; }

    public long ElapsedMilliseconds { get; set; }

    /// <summary>Trecho da resposta do modelo quando funcionou.</summary>
    public string? Reply { get; set; }

    /// <summary>Explicação para o administrador, sem segredo.</summary>
    public string Message { get; set; } = string.Empty;
}
