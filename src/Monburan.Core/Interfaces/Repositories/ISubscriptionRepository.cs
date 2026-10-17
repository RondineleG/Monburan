using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface ISubscriptionRepository : IBaseRepository<Subscription>
{
    Task<RequestResult<IEnumerable<Subscription>>> GetByAdvertiserIdAsync(Id advertiserId);

    /// <summary>Assinatura ativa do anunciante (com o plano carregado) ou <c>null</c>.</summary>
    Task<RequestResult<Subscription?>> GetActiveByAdvertiserIdAsync(Id advertiserId);
}
