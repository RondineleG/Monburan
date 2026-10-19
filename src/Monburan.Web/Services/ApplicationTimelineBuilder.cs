using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>Uma etapa da linha do tempo da candidatura, pronta para exibição.</summary>
public sealed record ApplicationTimelineStep(string Status, string Label, DateTime Date, string? Note, string? Responsible);

/// <summary>Monta a linha do tempo de etapas da candidatura a partir do histórico. Regra pura, testada em unitário.</summary>
public static class ApplicationTimelineBuilder
{
    /// <summary>Etapas da mais antiga para a mais nova, com notas vazias virando nulo.</summary>
    public static IReadOnlyList<ApplicationTimelineStep> Build(IEnumerable<ApplicationHistoryItemResponse> history)
    {
        return history
            .OrderBy(item => item.Date)
            .Select(item => new ApplicationTimelineStep(
                item.Status,
                UiLabels.ApplicationStatus(item.Status),
                item.Date,
                Clean(item.Note),
                Clean(item.Responsible)))
            .ToList();
    }

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
