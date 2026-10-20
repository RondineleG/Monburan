using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Interfaces.Repositories;

public interface ISubscriptionRepository : IBaseRepository<Subscription>
{
    Task<RequestResult<IEnumerable<Subscription>>> GetByAdvertiserIdAsync(Id advertiserId, CancellationToken cancellationToken = default);

    /// <summary>Assinatura ativa do anunciante (com o plano carregado) ou <c>null</c>.</summary>
    Task<RequestResult<Subscription?>> GetActiveByAdvertiserIdAsync(Id advertiserId, CancellationToken cancellationToken = default);

    /// <summary>Assinatura ativa do candidato (com o plano carregado) ou <c>null</c>.</summary>
    Task<RequestResult<Subscription?>> GetActiveByCandidateIdAsync(Id candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela a assinatura e devolve o anunciante ao plano inicial, na mesma transação:
    /// as duas gravações acontecem juntas ou nenhuma acontece. Sem isso, uma falha entre as
    /// duas deixava o plano do anunciante incoerente com a assinatura.
    /// </summary>
    /// <param name="subscription">Assinatura com o novo status, a data de fim e o motivo já preenchidos.</param>
    /// <param name="owner">Perfil dono da assinatura (empresa ou candidato) com o plano ativo que deve ser gravado.</param>
    /// <param name="fallbackPlan">Nome do plano para onde o perfil volta.</param>
    Task<RequestResult<IHasActivePlan>> CancelAsync(Subscription subscription, IHasActivePlan owner, string fallbackPlan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca o plano do dono da assinatura: cancela a assinatura anterior (quando existe), grava a nova
    /// e atualiza o plano ativo do perfil — tudo na mesma transação, para o perfil nunca
    /// ficar sem assinatura ativa nem com duas assinaturas ativas ao mesmo tempo.
    /// </summary>
    /// <param name="previousSubscription">Assinatura a cancelar, com status, data de fim e motivo já preenchidos.</param>
    /// <param name="newSubscription">Nova assinatura a gravar.</param>
    /// <param name="owner">Perfil dono da assinatura (empresa ou candidato) com o plano ativo que deve ser gravado.</param>
    Task<RequestResult<Subscription>> ChangePlanAsync(
        Subscription? previousSubscription,
        Subscription newSubscription,
        IHasActivePlan owner,
        CancellationToken cancellationToken = default);
}
