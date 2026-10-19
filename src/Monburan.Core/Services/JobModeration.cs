using Monburan.Core.Entities;
using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>O que a moderação mostra à empresa sobre uma vaga fora do ar.</summary>
public static class JobModeration
{
    /// <summary>
    /// Motivo informado pela moderação, só enquanto a vaga está oculta ou rejeitada. Em vaga ativa vem vazio, para a
    /// resposta pública da API nunca carregar texto de moderação.
    /// </summary>
    public static string? VisibleReason(Job job) =>
        job.Status is EJobStatus.Hidden or EJobStatus.Rejected && !string.IsNullOrWhiteSpace(job.Moderation?.Reason)
            ? job.Moderation!.Reason
            : null;
}
