using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

public interface INotificationRepository : IBaseRepository<Notification>
{
    /// <summary>Notificações mais recentes do destinatário (id do perfil candidato/anunciante).</summary>
    Task<RequestResult<IEnumerable<Notification>>> ListByRecipientAsync(string recipientId, int take, CancellationToken cancellationToken = default);

    /// <summary>Verifica se a combinação alerta/vaga já gerou notificação ativa.</summary>
    Task<RequestResult<bool>> ExistsJobAlertNotificationAsync(Guid jobAlertId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Marca como lidas todas as notificações não lidas do destinatário; retorna quantas mudaram.</summary>
    Task<RequestResult<int>> MarkAllAsReadAsync(string recipientId, CancellationToken cancellationToken = default);
}
