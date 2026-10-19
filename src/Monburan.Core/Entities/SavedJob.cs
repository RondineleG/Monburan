using Monburan.Core.Entities.Base;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>Vaga salva por um candidato. A linha é apagada ao dessalvar: o índice único (candidato, vaga) não convive com exclusão lógica.</summary>
public class SavedJob : BaseEntity
{
    public Id CandidateId { get; set; } = Id.Empty;

    public virtual Job? Job { get; set; }

    public Id JobId { get; set; } = Id.Empty;
}
