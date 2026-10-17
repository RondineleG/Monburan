using Monburan.Core.Requests;

namespace Monburan.Web.Services;

/// <summary>
/// Converte <see cref="RequestResult"/> em mensagens para o usuário — o equivalente do
/// <c>ResultMapper</c> da API para as páginas Blazor. Regras:
/// <list type="bullet">
/// <item>validação → mensagens dos campos (o <c>Message</c> vem vazio nesses casos);</item>
/// <item>regra de negócio, não encontrado e conflito → mensagem de domínio (segura para exibir);</item>
/// <item>erro interno → texto genérico; o detalhe técnico só vai para o log.</item>
/// </list>
/// </summary>
public static class ResultMessages
{
    public const string GenericError = "Não foi possível concluir a operação. Tente novamente.";

    public static string ToUserMessage(RequestResult result, string fallback = GenericError)
    {
        var validation = ValidationMessages(result);
        if (validation.Count > 0)
            return string.Join(" ", validation);

        return result.Status switch
        {
            EResultStatus.DomainError => NonEmpty(result.Message, fallback),
            EResultStatus.EntityNotFound or EResultStatus.EntityAlreadyExists =>
                NonEmpty(result.RequestEntityWarning?.Message, NonEmpty(result.Message, fallback)),
            _ => fallback
        };
    }

    /// <summary>Erros de validação agrupados por campo (chave vazia = erro geral).</summary>
    public static IReadOnlyDictionary<string, string[]> FieldErrors(RequestResult result)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in result.ValidationResult.Errors)
            Add(errors, error.Field, error.Message);

        foreach (var validation in result.Validations)
            Add(errors, validation.PropertyName, validation.Description);

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registra no log o detalhe de falhas internas — a página mostra só a mensagem genérica.
    /// </summary>
    public static void LogFailure(this ILogger logger, RequestResult result, string operation)
    {
        if (result.Status is EResultStatus.HasError or EResultStatus.EntityHasError)
        {
            logger.LogError("{Operation} falhou ({Status}): {Detail}",
                operation, result.Status, string.IsNullOrWhiteSpace(result.Message) ? result.ToString() : result.Message);
        }
    }

    private static List<string> ValidationMessages(RequestResult result) =>
        result.ValidationResult.Errors.Select(e => e.Message)
            .Concat(result.Validations.Select(v => v.Description))
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();

    private static void Add(Dictionary<string, List<string>> errors, string? field, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var key = field ?? string.Empty;
        if (!errors.TryGetValue(key, out var list))
            errors[key] = list = [];

        if (!list.Contains(message))
            list.Add(message);
    }

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
