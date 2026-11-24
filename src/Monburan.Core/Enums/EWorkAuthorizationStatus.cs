namespace Monburan.Core.Enums;

/// <summary>
/// Situação de autorização de trabalho AUTODECLARADA pelo candidato. Dado opcional —
/// a plataforma não determina elegibilidade legal para trabalhar em um país.
/// </summary>
public enum EWorkAuthorizationStatus
{
    /// <summary>Pessoa não informou — permanece distinto de "não tem".</summary>
    NotInformed,
    /// <summary>Declara não ter autorização de trabalho no exterior.</summary>
    None,
    /// <summary>Cidadania do país de destino.</summary>
    Citizen,
    /// <summary>Residência permanente.</summary>
    PermanentResidency,
    /// <summary>Visto de trabalho vigente.</summary>
    WorkVisa,
    /// <summary>Outra situação não listada.</summary>
    Other
}
