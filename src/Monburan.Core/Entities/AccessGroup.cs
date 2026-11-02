using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>
/// Grupo de acesso que pode ser atribuído a contas (ex.: "candidato-beta", "recrutador-beta"). O vínculo com a conta é uma
/// claim <see cref="Services.AccessGroups.ClaimType"/> do Identity. Os grupos implícitos (admin, produto, candidato,
/// recrutador) não ficam nesta tabela: saem do papel e do perfil da conta.
/// </summary>
public class AccessGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
