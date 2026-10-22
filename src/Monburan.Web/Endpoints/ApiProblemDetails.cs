namespace Monburan.Web.Endpoints;

/// <summary>
/// Padroniza os ProblemDetails gerados pelo framework (exception handler, status code
/// pages, falhas de autenticação): os títulos padrão vêm em inglês ("Not Found",
/// "An error occurred while processing your request.") e o app exibe o título/detalhe.
/// Erros 5xx nunca carregam detalhe técnico.
/// </summary>
internal static class ApiProblemDetails
{
    public const string GenericServerError = "Algo deu errado do nosso lado. Tente novamente em instantes.";

    // Títulos que o ASP.NET Core preenche sozinho (ProblemDetailsDefaults)
    private static readonly HashSet<string> FrameworkTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bad Request", "Unauthorized", "Forbidden", "Not Found", "Method Not Allowed", "Not Acceptable",
        "Request Timeout", "Conflict", "Precondition Failed", "Payload Too Large", "Unsupported Media Type",
        "Unprocessable Entity", "Upgrade Required", "Too Many Requests", "Bad Gateway", "Service Unavailable",
        "Gateway Timeout", "An error occurred while processing your request.", "One or more validation errors occurred."
    };

    public static void Localize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        if (string.IsNullOrWhiteSpace(problem.Title) || FrameworkTitles.Contains(problem.Title))
            problem.Title = TitleFor(status);

        // A extensão "exception" carrega a stack trace (e cabeçalhos, rota e route values) da
        // exceção. Ela nunca sai para o cliente, em nenhum status: um 400 ou 404 com exceção
        // anexada também vaza detalhe interno (e o corpo cresce de ~170 bytes para ~2 KB).
        if (context.Exception is not null)
            problem.Extensions.Remove("exception");

        if (status >= StatusCodes.Status500InternalServerError)
        {
            // Mensagem de exceção nunca vai para o cliente (pode conter SQL, paths, segredos)
            if (string.IsNullOrWhiteSpace(problem.Detail) || context.Exception is not null)
                problem.Detail = GenericServerError;

            problem.Extensions.Remove("exception");
        }
    }

    public static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida",
        StatusCodes.Status401Unauthorized => "Não autenticado",
        StatusCodes.Status403Forbidden => "Acesso negado",
        StatusCodes.Status404NotFound => "Recurso não encontrado",
        StatusCodes.Status405MethodNotAllowed => "Método não permitido",
        StatusCodes.Status409Conflict => "Conflito",
        StatusCodes.Status413PayloadTooLarge => "Arquivo muito grande",
        StatusCodes.Status415UnsupportedMediaType => "Formato não suportado",
        StatusCodes.Status422UnprocessableEntity => "Erro de validação",
        StatusCodes.Status429TooManyRequests => "Muitas requisições",
        >= StatusCodes.Status500InternalServerError => "Erro interno",
        _ => "Não foi possível concluir a operação"
    };

    /// <summary>Resposta de falha de autenticação/autorização do bearer (antes: <c>{ "error": "Unauthorized" }</c>).</summary>
    public static async Task WriteAsync(HttpContext httpContext, int status, string detail)
    {
        httpContext.Response.StatusCode = status;
        var problems = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Title = TitleFor(status),
                Detail = detail
            }
        });
    }
}
