using Monburan.Core.Entities.Base;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Imagem enviada como evidência de uma sugestão. Guardada em armazenamento privado.</summary>
public class JobSubmissionEvidence : BaseEntity
{
    public Id SubmissionId { get; set; } = null!;

    public virtual JobSubmission? Submission { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>SHA-256 do conteúdo, usado na busca de duplicidade.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Chave do arquivo no armazenamento privado (nunca um caminho público).</summary>
    public string StorageKey { get; set; } = string.Empty;
}
