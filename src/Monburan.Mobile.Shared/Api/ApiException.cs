using System.Net;

namespace Monburan.Mobile.Shared.Api;

/// <summary>
/// Erro de chamada à API. Carrega status HTTP, mensagem amigável e erros de validação.
/// </summary>
public class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string message,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ValidationErrors = validationErrors;
    }

    private ApiException(string message) : base(message)
    {
        IsNetworkError = true;
    }

    public HttpStatusCode? StatusCode { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    /// <summary>Falha de rede/offline — sem resposta do servidor.</summary>
    public bool IsNetworkError { get; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    public static ApiException Network(string? message = null) =>
        new(message ?? "Sem conexão com o servidor. Verifique sua internet e tente novamente.");
}
