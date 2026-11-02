using Monburan.Core.Entities.Base;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.Entities;

/// <summary>
/// Link público e individual de um currículo. Guarda só o hash do token (o token aparece uma única vez, na criação),
/// os campos que o candidato escolheu mostrar e a validade. Nunca expõe o identificador do currículo.
/// </summary>
public class ResumeShare : BaseEntity
{
    public Id ResumeId { get; set; } = null!;

    public virtual Resume? Resume { get; set; }

    /// <summary>SHA-256 do token. O token em si não é guardado.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Rótulo para o candidato identificar o link (ex.: "Vaga de motorista").</summary>
    public string? Label { get; set; }

    public bool ShowName { get; set; }

    public bool ShowSummary { get; set; }

    public bool ShowExperience { get; set; }

    public bool ShowEducation { get; set; }

    public bool ShowSkills { get; set; }

    public bool ShowLanguages { get; set; }

    /// <summary>Validade opcional; nulo significa sem prazo.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Quando foi revogado pelo candidato; revogação é imediata e não apaga o currículo.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsAvailableAt(DateTime nowUtc) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > nowUtc);
}
