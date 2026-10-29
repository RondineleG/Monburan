namespace Monburan.Core.DTOs.Request;

/// <summary>Sugestão anônima enviada por quem encontrou a vaga. Nenhum campo de conta é obrigatório.</summary>
public sealed class SubmitJobSuggestionRequest
{
    public string Title { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Description { get; set; }

    public string? SourceUrl { get; set; }

    public string? SourceDescription { get; set; }

    public string? ContactEmail { get; set; }

    public IReadOnlyList<SubmissionEvidenceInput> Evidences { get; set; } = [];

    /// <summary>Chave de quem envia (IP ou sessão). É convertida em hash antes de ser usada ou guardada.</summary>
    public string SubmitterKey { get; set; } = string.Empty;
}

/// <summary>Imagem enviada como evidência. O tipo é conferido pela assinatura do conteúdo, não só pelo nome.</summary>
public sealed record SubmissionEvidenceInput(string FileName, string ContentType, byte[] Content);
