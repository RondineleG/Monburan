namespace Monburan.Core.DTOs.Response;

/// <summary>Campo extraído por IA: o valor encontrado na evidência (nulo quando não há) e o nível de confiança.</summary>
/// <param name="Value">Texto encontrado na imagem, ou nulo quando a imagem não traz a informação.</param>
/// <param name="Confidence">"high", "medium", "low" ou "none" (quando não há valor).</param>
public sealed record AiDraftField(string? Value, string Confidence);

/// <summary>
/// Rascunho da vaga extraído da evidência. É sugestão para o moderador: não publica nada e não altera a sugestão
/// além de guardar o próprio rascunho.
/// </summary>
/// <param name="Fields">Campos por nome (ver <see cref="Monburan.Core.Services.AiJobDraftParser.Fields"/>).</param>
/// <param name="NeedsReview">Há campos obrigatórios vazios ou com confiança baixa; o moderador precisa conferir.</param>
public sealed record AiJobDraft(IReadOnlyDictionary<string, AiDraftField> Fields, bool NeedsReview);
