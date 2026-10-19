using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Perfil de carreira do candidato: um por candidato, base das recomendações de vagas.</summary>
public class CareerProfile : BaseEntity
{
    public Id CandidateId { get; set; } = Id.Empty;

    public string? Category { get; set; }

    public string? City { get; set; }

    public EContractType? ContractType { get; set; }

    public string? DesiredRole { get; set; }

    public decimal? MinimumSalary { get; set; }

    public string? Mode { get; set; }

    public string? State { get; set; }
}
