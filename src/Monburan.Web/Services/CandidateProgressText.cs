using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>Textos do progresso do candidato. Regra pura, testada em unitário.</summary>
public static class CandidateProgressText
{
    /// <summary>Rótulo do progresso de completude, ex.: "Perfil 67% completo".</summary>
    public static string Completeness(int percent) =>
        $"Perfil {Math.Clamp(percent, 0, 100)}% completo";

    /// <summary>Próximos passos: itens do checklist que ainda não foram feitos.</summary>
    public static IReadOnlyList<CandidateProgressChecklistItem> NextSteps(CandidateProgressResponse progress) =>
        progress.Checklist.Where(item => !item.Done).ToList();
}
