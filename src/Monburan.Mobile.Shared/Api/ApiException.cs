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

    /// <summary>Primeiro erro de validação do campo (chave do ProblemDetails, sem diferenciar maiúsculas).</summary>
    public string? FieldError(string field) =>
        ValidationErrors?
            .Where(e => string.Equals(e.Key, field, StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => e.Value)
            .FirstOrDefault();

    public static ApiException Network(string? message = null) =>
        new(message ?? "Sem conexão com o servidor. Verifique sua internet e tente novamente.");

    /// <summary>
    /// Servidor respondeu algo que não é o contrato esperado (ex.: HTML de um proxy/manutenção).
    /// Vira ApiException para as telas tratarem como erro comum, em vez de quebrar.
    /// </summary>
    public static ApiException InvalidResponse(HttpStatusCode statusCode) =>
        new(statusCode, "Recebemos uma resposta inesperada do servidor. Tente novamente em instantes.");
}
