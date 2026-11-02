namespace Monburan.Core.DTOs.Response;

/// <summary>Possível duplicata mostrada ao moderador. A detecção ajuda a decidir; nunca decide sozinha.</summary>
/// <param name="Kind">"Vaga" (já publicada) ou "Sugestão" (já recebida).</param>
/// <param name="Id">Identificador do item encontrado.</param>
/// <param name="Title">Título do item encontrado.</param>
/// <param name="Reason">Sinais que coincidiram (empresa, título, cidade, fonte ou evidência).</param>
public sealed record DuplicateCandidate(string Kind, Guid Id, string Title, string Reason);
