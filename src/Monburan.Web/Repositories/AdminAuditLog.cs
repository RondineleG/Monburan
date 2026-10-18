using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
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
        // Também no log da aplicação: se o banco falhar, a ação ainda fica registrada em algum lugar
        _logger.LogInformation(
            "Auditoria: {Actor} fez {Action} em {TargetType} {TargetId} ({TargetLabel})",
            record.Actor.Email, record.Action, record.TargetType, record.TargetId, record.TargetLabel);

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
            _logger.LogError(exception, "Auditoria: não foi possível gravar {Action} de {Actor}", record.Action, record.Actor.Email);
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
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(entry =>
                entry.ActorEmail.ToLower().Contains(term)
                || entry.TargetLabel.ToLower().Contains(term)
                || (entry.Details != null && entry.Details.ToLower().Contains(term)));
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
