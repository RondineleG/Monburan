using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.Ai;

internal static class AiProviderMapper
{
    public static AiProviderResponse ToResponse(AiProvider provider, bool keyConfigured) => new()
    {
        Id = provider.Id.Value,
        Name = provider.Name,
        Protocol = provider.Protocol,
        BaseUrl = provider.BaseUrl,
        Model = provider.Model,
        ApiKeyEnvVar = provider.ApiKeyEnvVar,
        KeyConfigured = keyConfigured,
        IsEnabled = provider.IsEnabled,
        IsDefault = provider.IsDefault,
        TimeoutSeconds = provider.TimeoutSeconds,
        MaxOutputTokens = provider.MaxOutputTokens,
        Temperature = provider.Temperature,
        SendTemperature = provider.SendTemperature,
        UseJsonResponseFormat = provider.UseJsonResponseFormat,
        SupportsVision = provider.SupportsVision,
        UseMaxCompletionTokens = provider.UseMaxCompletionTokens,
        UpdatedAt = provider.UpdatedAt,
        UpdatedBy = provider.UpdatedBy
    };
}
