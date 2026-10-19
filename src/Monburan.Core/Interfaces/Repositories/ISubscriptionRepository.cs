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

    /// <summary>
    /// Cancela a assinatura e devolve o anunciante ao plano inicial, na mesma transação:
    /// as duas gravações acontecem juntas ou nenhuma acontece. Sem isso, uma falha entre as
    /// duas deixava o plano do anunciante incoerente com a assinatura.
    /// </summary>
    /// <param name="subscription">Assinatura com o novo status, a data de fim e o motivo já preenchidos.</param>
    /// <param name="advertiser">Anunciante com o plano ativo que deve ser gravado.</param>
    /// <param name="fallbackPlan">Nome do plano para onde o anunciante volta.</param>
    Task<RequestResult<Advertiser>> CancelAsync(Subscription subscription, Advertiser advertiser, string fallbackPlan);

    /// <summary>
    /// Troca o plano do anunciante: cancela a assinatura anterior (quando existe), grava a nova
    /// e atualiza o plano ativo do anunciante — tudo na mesma transação, para o anunciante nunca
    /// ficar sem assinatura ativa nem com duas assinaturas ativas ao mesmo tempo.
    /// </summary>
    /// <param name="previousSubscription">Assinatura a cancelar, com status, data de fim e motivo já preenchidos.</param>
    /// <param name="newSubscription">Nova assinatura a gravar.</param>
    /// <param name="advertiser">Anunciante com o plano ativo que deve ser gravado.</param>
    Task<RequestResult<Subscription>> ChangePlanAsync(
        Subscription? previousSubscription,
        Subscription newSubscription,
        Advertiser advertiser);
}
