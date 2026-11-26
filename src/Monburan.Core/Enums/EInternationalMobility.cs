namespace Monburan.Core.Enums;

/// <summary>Disponibilidade autodeclarada do candidato para trabalho internacional (remoto/relocação).</summary>
public enum EInternationalMobility
{
    /// <summary>Pessoa não informou — permanece distinto de "não pode".</summary>
    NotInformed,
    /// <summary>Aceita apenas trabalho remoto para empresa estrangeira.</summary>
    RemoteOnly,
    /// <summary>Aceita apenas com realocação para o país da vaga.</summary>
    Relocation,
    /// <summary>Aceita remoto ou realocação.</summary>
    RemoteOrRelocation
}
