using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>
/// Funcionalidade liberada por grupo de acesso. Serve para colocar código em produção desligado e abrir aos poucos (time,
/// beta, todos). É temporária: depois de liberada para todos, a flag sai do código. Regra de negócio permanente ("quem paga
/// tem IA") fica nos planos, não aqui.
/// </summary>
public class FeatureFlag : BaseEntity
{
    /// <summary>Chave usada no código, em minúsculas com hífen (ex.: "nova-busca-de-vagas").</summary>
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Liberada para todo mundo, inclusive visitantes. Último passo antes de remover a flag do código.</summary>
    public bool EnabledForEveryone { get; set; }

    /// <summary>Grupos com acesso, separados por vírgula (ver <see cref="Services.AccessGroups"/>).</summary>
    public string Groups { get; set; } = string.Empty;

    /// <summary>Quando a flag deve sair do código; a tela destaca as atrasadas.</summary>
    public DateTime? RemoveBy { get; set; }

    public IReadOnlyList<string> GroupList =>
        Groups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
