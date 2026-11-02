namespace Monburan.Core.DTOs.Request;

/// <summary>Pede o rascunho da vaga de uma sugestão a partir das suas imagens.</summary>
public sealed record ExtractJobDraftRequest(Guid SubmissionId);
