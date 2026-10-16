using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monburan.Mobile.Shared.Api;

internal static class ApiJson
{
    // Mesma convenção da API: enums serializam como string (ex.: NotificationResponse.Type)
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
