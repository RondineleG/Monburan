using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

/// <summary>Lista as candidaturas do candidato autenticado.</summary>
public class ListApplicationsByCandidateRequest
{
    [Required]
    public Guid CandidateId { get; set; }

    /// <summary>Página pedida (começa em 1).</summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Quantidade por página. Em 0 (padrão) devolve até o teto da listagem; com valor maior,
    /// devolve a página pedida. O teto evita materializar dezenas de milhares de candidaturas.
    /// </summary>
    public int Size { get; set; }
}
