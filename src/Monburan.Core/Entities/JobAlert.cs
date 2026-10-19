using Monburan.Core.Entities.Base;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Busca salva do candidato: avisa por notificação quando uma vaga nova casa com os filtros.</summary>
public class JobAlert : BaseEntity
{
    public Id CandidateId { get; set; } = Id.Empty;

    public string? Category { get; set; }

    public string? City { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Keywords { get; set; }

    public DateTime? LastMatchedAt { get; set; }

    public string? Mode { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal? SalaryMin { get; set; }

    public string? State { get; set; }
}
