namespace Monburan.Core.Enums;

public enum EResultStatus
{
    Success,

    HasValidation,

    HasError,

    EntityNotFound,

    EntityHasError,

    EntityAlreadyExists,

    // Erro de domínio com mensagem segura para o usuário (não carrega internals de exceção)
    DomainError,

    NoContent,
}