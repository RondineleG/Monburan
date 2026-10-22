using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public sealed class AdminAuditLog : IAdminAuditLog
{
    private const int TargetLabelMaxLength = 200;
    private const int DetailsMaxLength = 1000;
    private const int CreatedByMaxLength = 100; // limite da coluna herdada do BaseEntity

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<AdminAuditLog> _logger;

    public AdminAuditLog(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<AdminAuditLog> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
    {
        // Também no log da aplicação: se o banco falhar, a ação ainda fica registrada em algum lugar. Só identificadores e o
        // nome da ação: e-mail e nome das pessoas ficam na tabela de auditoria, não nos arquivos de log
        _logger.LogInformation(
            "Auditoria: {ActorId} fez {Action} em {TargetType} {TargetId}",
            record.Actor.Id, AdminAuditActions.Label(record.Action), record.TargetType, record.TargetId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.AdminAuditEntries.Add(new AdminAuditEntry
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = "default",
                Action = record.Action,
                ActorId = record.Actor.Id,
                ActorEmail = record.Actor.Email,
                CreatedBy = Truncate(record.Actor.Email, CreatedByMaxLength),
                TargetType = record.TargetType,
                TargetId = record.TargetId,
                TargetLabel = Truncate(record.TargetLabel, TargetLabelMaxLength),
                Details = record.Details is null ? null : Truncate(record.Details, DetailsMaxLength)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Auditoria: não foi possível gravar {Action} de {ActorId}", AdminAuditActions.Label(record.Action), record.Actor.Id);
        }
    }

    public async Task<PagedResponse<AdminAuditEntry>> ListAsync(
        AdminAuditFilter filter, int page, int size, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        size = size is < 1 or > 100 ? 25 : size;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.AdminAuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(entry => entry.Action == filter.Action);

        if (!string.IsNullOrWhiteSpace(filter.TargetType))
            query = query.Where(entry => entry.TargetType == filter.TargetType);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            // No Postgres a busca usa ILike, que aproveita o índice de trigramas; no InMemory
            // (testes de API) cai para ToLower().Contains, que o provider sabe traduzir.
            if (TextSearch.SupportsILike(context))
            {
                var pattern = TextSearch.Pattern(term);
                query = query.Where(entry =>
                    EF.Functions.ILike(entry.ActorEmail, pattern)
                    || EF.Functions.ILike(entry.TargetLabel, pattern)
                    || (entry.Details != null && EF.Functions.ILike(entry.Details, pattern)));
            }
            else
            {
                var lowered = term.ToLower();
                query = query.Where(entry =>
                    entry.ActorEmail.ToLower().Contains(lowered)
                    || entry.TargetLabel.ToLower().Contains(lowered)
                    || (entry.Details != null && entry.Details.ToLower().Contains(lowered)));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(entry => entry.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminAuditEntry> { Data = items, TotalCount = total, Page = page, Size = size };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
