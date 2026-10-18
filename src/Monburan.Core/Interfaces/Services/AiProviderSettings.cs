using Monburan.Core.Entities;
using Monburan.Core.Enums;

namespace Monburan.Core.Interfaces.Services;

/// <summary>Configuração de um provedor como o cliente de IA a usa (sem a chave, que vem do ambiente na hora da chamada).</summary>
public sealed record AiProviderSettings(
    Guid Id,
    string Name,
    EAiProtocol Protocol,
    string BaseUrl,
    string Model,
    string ApiKeyEnvVar,
    bool IsEnabled,
    bool IsDefault,
    int TimeoutSeconds,
    int MaxOutputTokens,
    double Temperature,
    bool SendTemperature,
    bool UseJsonResponseFormat,
    bool UseMaxCompletionTokens)
{
    public static AiProviderSettings From(AiProvider provider) => new(
        provider.Id.Value,
        provider.Name,
        provider.Protocol,
        provider.BaseUrl,
        provider.Model,
        provider.ApiKeyEnvVar,
        provider.IsEnabled,
        provider.IsDefault,
        provider.TimeoutSeconds,
        provider.MaxOutputTokens,
        provider.Temperature,
        provider.SendTemperature,
        provider.UseJsonResponseFormat,
        provider.UseMaxCompletionTokens);
}
