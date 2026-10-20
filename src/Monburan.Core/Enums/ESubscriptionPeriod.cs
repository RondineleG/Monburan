namespace Monburan.Core.Enums;

/// <summary>Ciclo de cobrança do plano. <c>None</c> = plano sem vigência (ex.: Free), que não gera data de fim.</summary>
public enum ESubscriptionPeriod
{
    None,
    Monthly,
    Annual
}
