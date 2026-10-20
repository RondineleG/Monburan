using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class AddressRepository
{
    public AddressRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public async Task AddAsync(Address address, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Addresses.Add(address);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (!Id.TryCreate(id, out var key))
            return;

        var address = await context.Addresses.FindAsync([key], cancellationToken);
        if (address != null)
        {
            context.Addresses.Remove(address);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<Address>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Addresses.ToListAsync(cancellationToken);
    }

    public async Task<List<Address>> GetOwnedAsync(Guid? candidateId, Guid? advertiserId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownedIds = await GetOwnedIdsAsync(candidateId, advertiserId);
        if (ownedIds.Count == 0)
            return [];

        return await context.Addresses
            .Where(a => ownedIds.Contains(a.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<Id>> GetOwnedIdsAsync(Guid? candidateId, Guid? advertiserId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownedIds = new HashSet<Id>();

        if (candidateId.HasValue && Id.TryCreate(candidateId.Value, out var candidateKey))
        {
            var addressId = await context.Candidates
                .Where(c => c.Id == candidateKey && !c.IsDeleted)
                .Select(c => c.AddressId)
                .FirstOrDefaultAsync(cancellationToken);
            if (addressId is not null)
                ownedIds.Add(addressId);
        }

        if (advertiserId.HasValue && Id.TryCreate(advertiserId.Value, out var advertiserKey))
        {
            var addressId = await context.Advertisers
                .Where(a => a.Id == advertiserKey && !a.IsDeleted)
                .Select(a => a.AddressId)
                .FirstOrDefaultAsync(cancellationToken);
            if (addressId is not null)
                ownedIds.Add(addressId);
        }

        return ownedIds;
    }

    public async Task LinkToOwnerAsync(Id addressId, Guid? candidateId, Guid? advertiserId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (candidateId.HasValue && Id.TryCreate(candidateId.Value, out var candidateKey))
        {
            var candidate = await context.Candidates.FindAsync([candidateKey], cancellationToken);
            if (candidate is not null)
                candidate.AddressId = addressId;
        }

        if (advertiserId.HasValue && Id.TryCreate(advertiserId.Value, out var advertiserKey))
        {
            var advertiser = await context.Advertisers.FindAsync([advertiserKey], cancellationToken);
            if (advertiser is not null)
                advertiser.AddressId = addressId;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkFromOwnerAsync(Id addressId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.Candidates
            .Where(c => c.AddressId == addressId)
            .ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
            candidate.AddressId = null;

        var advertisers = await context.Advertisers
            .Where(a => a.AddressId == addressId)
            .ToListAsync(cancellationToken);
        foreach (var advertiser in advertisers)
            advertiser.AddressId = null;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return Id.TryCreate(id, out var key)
            ? await context.Addresses.FindAsync([key], cancellationToken)
            : null;
    }

    public async Task UpdateAsync(Address address, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Addresses.Update(address);
        await context.SaveChangesAsync(cancellationToken);
    }
}
