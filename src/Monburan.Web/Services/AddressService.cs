using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Services;

public class AddressService
{
    public AddressService(AddressRepository repository)
    {
        _repository = repository;
    }

    private readonly AddressRepository _repository;

    public Task AddAsync(Address address) => _repository.AddAsync(address);

    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task<List<Address>> GetAllAsync() => _repository.GetAllAsync();

    public Task<List<Address>> GetOwnedAsync(Guid? candidateId, Guid? advertiserId) =>
        _repository.GetOwnedAsync(candidateId, advertiserId);

    public Task<HashSet<Id>> GetOwnedIdsAsync(Guid? candidateId, Guid? advertiserId) =>
        _repository.GetOwnedIdsAsync(candidateId, advertiserId);

    public Task LinkToOwnerAsync(Id addressId, Guid? candidateId, Guid? advertiserId) =>
        _repository.LinkToOwnerAsync(addressId, candidateId, advertiserId);

    public Task UnlinkFromOwnerAsync(Id addressId) =>
        _repository.UnlinkFromOwnerAsync(addressId);

    public Task<Address?> GetByIdAsync(Guid id) => _repository.GetByIdAsync(id);

    public Task UpdateAsync(Address address) => _repository.UpdateAsync(address);
}
