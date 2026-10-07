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

    public async Task AddAsync(Address address)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Addresses.Add(address);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (!Id.TryCreate(id, out var key))
            return;

        var address = await context.Addresses.FindAsync(key);
        if (address != null)
        {
            context.Addresses.Remove(address);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Address>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Addresses.ToListAsync();
    }

    public async Task<List<Address>> GetOwnedAsync(Guid? candidateId, Guid? advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ownedIds = await GetOwnedIdsAsync(candidateId, advertiserId);
        if (ownedIds.Count == 0)
            return [];

        return await context.Addresses
            .Where(a => ownedIds.Contains(a.Id))
            .ToListAsync();
    }

    public async Task<HashSet<Id>> GetOwnedIdsAsync(Guid? candidateId, Guid? advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ownedIds = new HashSet<Id>();

        if (candidateId.HasValue && Id.TryCreate(candidateId.Value, out var candidateKey))
        {
            var addressId = await context.Candidates
                .Where(c => c.Id == candidateKey && !c.IsDeleted)
                .Select(c => c.AddressId)
                .FirstOrDefaultAsync();
            if (addressId is not null)
                ownedIds.Add(addressId);
        }

        if (advertiserId.HasValue && Id.TryCreate(advertiserId.Value, out var advertiserKey))
        {
            var addressId = await context.Advertisers
                .Where(a => a.Id == advertiserKey && !a.IsDeleted)
                .Select(a => a.AddressId)
                .FirstOrDefaultAsync();
            if (addressId is not null)
                ownedIds.Add(addressId);
        }

        return ownedIds;
    }

    public async Task LinkToOwnerAsync(Id addressId, Guid? candidateId, Guid? advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (candidateId.HasValue && Id.TryCreate(candidateId.Value, out var candidateKey))
        {
            var candidate = await context.Candidates.FindAsync(candidateKey);
            if (candidate is not null)
                candidate.AddressId = addressId;
        }

        if (advertiserId.HasValue && Id.TryCreate(advertiserId.Value, out var advertiserKey))
        {
            var advertiser = await context.Advertisers.FindAsync(advertiserKey);
            if (advertiser is not null)
                advertiser.AddressId = addressId;
        }

        await context.SaveChangesAsync();
    }

    public async Task UnlinkFromOwnerAsync(Id addressId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidates = await context.Candidates
            .Where(c => c.AddressId == addressId)
            .ToListAsync();
        foreach (var candidate in candidates)
            candidate.AddressId = null;

        var advertisers = await context.Advertisers
            .Where(a => a.AddressId == addressId)
            .ToListAsync();
        foreach (var advertiser in advertisers)
            advertiser.AddressId = null;

        await context.SaveChangesAsync();
    }

    public async Task<Address?> GetByIdAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return Id.TryCreate(id, out var key)
            ? await context.Addresses.FindAsync(key)
            : null;
    }

    public async Task UpdateAsync(Address address)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Addresses.Update(address);
        await context.SaveChangesAsync();
    }
}
