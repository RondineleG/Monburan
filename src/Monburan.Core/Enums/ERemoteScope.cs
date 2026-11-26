namespace Monburan.Core.Enums;

/// <summary>Onde a pessoa pode estar para trabalhar remotamente na vaga. Nulo na vaga = não informado.</summary>
public enum ERemoteScope
{
    /// <summary>De qualquer país.</summary>
    Global,

    /// <summary>Só dos países listados em <c>Job.EligibleCountries</c>.</summary>
    Restricted
}
