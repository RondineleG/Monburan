using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Core.DTOs.Request;

/// <summary>
/// Decisão do moderador sobre uma sugestão. Na aprovação, o moderador confirma os dados e escolhe o canal de candidatura:
/// nada disso é inventado pelo sistema.
/// </summary>
public sealed class ModerateJobSubmissionRequest
{
    public Guid SubmissionId { get; set; }

    public AdminActor Actor { get; set; } = null!;

    public ESubmissionDecision Decision { get; set; }

    /// <summary>Motivo (rejeição), pedido de informação ou observação do registro.</summary>
    public string? Note { get; set; }

    // Aprovação: dados confirmados pelo moderador
    public string? CompanyName { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public EApplicationChannel? Channel { get; set; }

    public string? ApplicationUrl { get; set; }

    public string? ApplicationContact { get; set; }

    public string? ApplicationInstructions { get; set; }

    public string? CboCode { get; set; }

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public ESalaryPeriod? SalaryPeriod { get; set; }

    // Duplicidade: exatamente um dos dois
    public Guid? DuplicateOfJobId { get; set; }

    public Guid? DuplicateOfSubmissionId { get; set; }
}
