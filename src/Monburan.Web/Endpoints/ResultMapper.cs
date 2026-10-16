using Monburan.Core.Enums;
using Monburan.Core.Requests;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Mapeia <see cref="RequestResult"/>/<see cref="RequestResult{T}"/> (pattern Result
/// usado pelos UseCases) para respostas HTTP com ProblemDetails (RFC 7807).
/// </summary>
internal static class ResultMapper
{
    public static IResult ToHttpResult<T>(this RequestResult<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        // UseCases podem devolver erros de validação com outro status (ex.: NoContent);
        // erros de validação sempre viram 400 ValidationProblem.
        if (result.Status != EResultStatus.Success && HasValidationErrors(result))
            return ValidationProblem(result);

        return result.Status switch
        {
            EResultStatus.Success => SuccessResult(result.Data, successStatusCode),
            EResultStatus.NoContent => TypedResults.NoContent(),
            EResultStatus.HasValidation => TypedResults.ValidationProblem(
                ToValidationErrors(result),
                title: "Erro de validação"),
            EResultStatus.EntityNotFound => Problem(result, StatusCodes.Status404NotFound, "Recurso não encontrado"),
            EResultStatus.EntityAlreadyExists => Problem(result, StatusCodes.Status409Conflict, "Conflito"),
            EResultStatus.DomainError => Problem(result, StatusCodes.Status400BadRequest, "Não foi possível concluir a operação"),
            EResultStatus.EntityHasError or EResultStatus.HasError =>
                ErrorProblem(StatusCodes.Status400BadRequest, "Erro ao processar a operação"),
            _ => ErrorProblem(StatusCodes.Status500InternalServerError, "Erro interno"),
        };
    }

    public static IResult ToHttpResult(this RequestResult result)
    {
        if (result.Status != EResultStatus.Success && HasValidationErrors(result))
            return ValidationProblem(result);

        return result.Status switch
        {
            EResultStatus.Success => TypedResults.Ok(),
            EResultStatus.NoContent => TypedResults.NoContent(),
            EResultStatus.HasValidation => TypedResults.ValidationProblem(
                ToValidationErrors(result),
                title: "Erro de validação"),
            EResultStatus.EntityNotFound => Problem(result, StatusCodes.Status404NotFound, "Recurso não encontrado"),
            EResultStatus.EntityAlreadyExists => Problem(result, StatusCodes.Status409Conflict, "Conflito"),
            EResultStatus.DomainError => Problem(result, StatusCodes.Status400BadRequest, "Não foi possível concluir a operação"),
            EResultStatus.EntityHasError or EResultStatus.HasError =>
                ErrorProblem(StatusCodes.Status400BadRequest, "Erro ao processar a operação"),
            _ => ErrorProblem(StatusCodes.Status500InternalServerError, "Erro interno"),
        };
    }

    private static IResult SuccessResult(object? data, int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status201Created => TypedResults.Json(data, statusCode: StatusCodes.Status201Created),
            StatusCodes.Status204NoContent => TypedResults.NoContent(),
            _ => TypedResults.Ok(data),
        };
    }

    private static IResult Problem(RequestResult result, int statusCode, string title)
    {
        var extensions = new Dictionary<string, object?>();

        if (result.RequestEntityWarning is not null)
        {
            extensions["entity"] = result.RequestEntityWarning.Name;
            extensions["entityId"] = result.RequestEntityWarning.Id?.ToString();
        }

        if (result.GeneralErrors.Count != 0)
            extensions["errors"] = result.GeneralErrors;

        if (result.EntityErrors.Count != 0)
            extensions["entityErrors"] = result.EntityErrors;

        // Domain messages chegam por Message (DomainError) ou por
        // RequestEntityWarning.Message (NotFound/AlreadyExists) — nunca descartar
        var detail = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : result.RequestEntityWarning?.Message ?? title;

        return TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            extensions: extensions);
    }

    // Erros internos (ex.: exceções capturadas pelos UseCases) nunca expõem
    // Message/GeneralErrors — podem conter SQL, paths e outros internals.
    private static IResult ErrorProblem(int statusCode, string title) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            detail: "Não foi possível concluir a operação. Tente novamente.");

    private static bool HasValidationErrors(RequestResult result) =>
        result.ValidationResult.Errors.Count != 0 || result.Validations.Any();

    private static IResult ValidationProblem(RequestResult result) =>
        TypedResults.ValidationProblem(ToValidationErrors(result), title: "Erro de validação");

    private static Dictionary<string, string[]> ToValidationErrors(RequestResult result)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in result.ValidationResult.Errors)
        {
            var field = string.IsNullOrWhiteSpace(error.Field) ? string.Empty : error.Field;
            if (errors.TryGetValue(field, out var existing))
                errors[field] = [.. existing, error.Message];
            else
                errors[field] = [error.Message];
        }

        // Validations também podem vir preenchidas sem passar pelo ValidationResult
        foreach (var validation in result.Validations)
        {
            var field = string.IsNullOrWhiteSpace(validation.PropertyName) ? string.Empty : validation.PropertyName;
            if (errors.TryGetValue(field, out var existing))
            {
                if (!existing.Contains(validation.Description))
                    errors[field] = [.. existing, validation.Description];
            }
            else
            {
                errors[field] = [validation.Description];
            }
        }

        if (errors.Count == 0)
            errors[string.Empty] = ["Os dados enviados são inválidos."];

        return errors;
    }
}
