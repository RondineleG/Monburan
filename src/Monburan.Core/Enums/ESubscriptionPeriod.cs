namespace Monburan.Core.Enums;

/// <summary>
/// Ciclo de cobrança do plano. <c>None</c> = plano sem vigência (ex.: Free), que não gera data de fim.
/// <c>Monthly</c> é o primeiro para casar com o default do banco: o EF usa o default da coluna quando
/// a propriedade tem o valor default do CLR, e aqui os dois são o mesmo.
/// </summary>
public enum ESubscriptionPeriod
{
    Monthly,
    Annual,
    None
}
