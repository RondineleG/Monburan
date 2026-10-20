using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class AdvertiserRepository : IAdvertiserRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public AdvertiserRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Advertiser>> CreateAsync(Advertiser entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Advertisers.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Advertiser>.Success(entity);
    }

    public async Task<RequestResult<Advertiser>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var advertiser = await context.Advertisers.FindAsync([id], cancellationToken);
        if (advertiser == null)
            return RequestResult<Advertiser>.EntityNotFound("Advertiser", id.Value, "Anunciante não encontrado.");

        advertiser.IsDeleted = true;
        advertiser.DeletedAt = DateTime.UtcNow;
        
        context.Advertisers.Update(advertiser);
        await context.SaveChangesAsync(cancellationToken);
        return RequestResult<Advertiser>.Success(advertiser);
    }

    public async Task<RequestResult<IEnumerable<Advertiser>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var advertisers = await context.Advertisers
            .Include(a => a.Address)
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<IEnumerable<Advertiser>>.Success(advertisers);
    }

    public async Task<RequestResult<Advertiser>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var advertiser = await context.Advertisers
            .Include(a => a.Address)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);

        if (advertiser == null)
            return RequestResult<Advertiser>.EntityNotFound("Advertiser", id.Value, "Anunciante não encontrado.");

        return RequestResult<Advertiser>.Success(advertiser);
    }

    public async Task<RequestResult<Advertiser>> UpdateAsync(Advertiser entity, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Advertisers.Update(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // CPF/CNPJ é único (IX_Advertisers_TaxId); o e-mail não é editável pelo perfil
            return RequestResult<Advertiser>.WithValidationError(
                "Este CPF/CNPJ já está cadastrado em outra conta.", nameof(Advertiser.TaxId));
        }

        return RequestResult<Advertiser>.Success(entity);
    }

    public async Task<RequestResult<PagedResponse<Advertiser>>> SearchAsync(string? searchTerm, EAdvertiserStatus? status, int page, int size, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Advertisers
            .AsNoTracking()
            .Include(a => a.Address)
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // No Postgres a busca usa ILike, que aproveita o índice de trigramas; no InMemory
            // (testes de API) cai para ToLower().Contains, que o provider sabe traduzir.
            if (TextSearch.SupportsILike(context))
            {
                var pattern = TextSearch.Pattern(term);
                query = query.Where(a =>
                    EF.Functions.ILike(a.Name, pattern)
                    || EF.Functions.ILike(a.Email, pattern)
                    || (a.TaxId != null && EF.Functions.ILike(a.TaxId, pattern)));
            }
            else
            {
                var lowered = term.ToLower();
                query = query.Where(a =>
                    a.Name.ToLower().Contains(lowered)
                    || a.Email.ToLower().Contains(lowered)
                    || (a.TaxId != null && a.TaxId.ToLower().Contains(lowered)));
            }
        }

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        // Filtro antes da paginação — TotalCount é o total filtrado, não o tamanho da página
        var total = await query.CountAsync(cancellationToken);
        var advertisers = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return RequestResult<PagedResponse<Advertiser>>.Success(new PagedResponse<Advertiser>
        {
            Data = advertisers,
            TotalCount = total,
            Page = page,
            Size = size
        });
    }
}
