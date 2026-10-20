using System.Net;
using System.Net.Sockets;

namespace Monburan.Mobile.Shared.Api;

/// <summary>
/// Erro de chamada à API. Carrega status HTTP, mensagem amigável e erros de validação.
/// </summary>
public class ApiException : Exception
{
    /// <summary>Mensagem padrão de um 401 sem explicação do servidor.</summary>
    public const string SessionExpiredMessage = "Sessão expirada. Entre novamente.";

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

    /// <summary>
    /// Falha de transporte, sem resposta do servidor. Offline e DNS chegam como HttpRequestException; no Android, uma queda no
    /// meio da requisição (troca de Wi-Fi para 4G, sinal fraco) chega como WebException ou IOException.
    /// </summary>
    public static bool IsTransportFailure(Exception exception) =>
        exception is HttpRequestException or WebException or IOException or SocketException;

    public static ApiException Network(string? message = null) =>
        new(message ?? "Sem conexão com o servidor. Verifique sua internet e tente novamente.");

    /// <summary>
    /// Servidor respondeu algo que não é o contrato esperado (ex.: HTML de um proxy/manutenção).
    /// Vira ApiException para as telas tratarem como erro comum, em vez de quebrar.
    /// </summary>
    public static ApiException InvalidResponse(HttpStatusCode statusCode) =>
        new(statusCode, "Recebemos uma resposta inesperada do servidor. Tente novamente em instantes.");
}
