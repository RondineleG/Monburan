using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.Interfaces.Services;

/// <summary>Quem está agindo (identidade autenticada, nunca vinda do cliente).</summary>
public sealed record AdminActor(string Id, string Email);

public sealed record AdminAuditRecord(
    string Action,
    AdminActor Actor,
    string TargetType,
    string TargetId,
    string TargetLabel,
    string? Details = null);

public sealed record AdminAuditFilter(string? SearchTerm = null, string? Action = null, string? TargetType = null);

/// <summary>Trilha de auditoria das ações de administração.</summary>
public interface IAdminAuditLog
{
    /// <summary>
    /// Registra a ação. Uma falha do banco é registrada no log e não derruba a ação do administrador: não poder inativar uma
    /// conta abusiva por causa da auditoria seria pior do que perder uma linha da trilha (o log da aplicação ainda a tem).
    /// </summary>
    Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Mais recentes primeiro.</summary>
    Task<PagedResponse<AdminAuditEntry>> ListAsync(AdminAuditFilter filter, int page, int size, CancellationToken cancellationToken = default);
}
