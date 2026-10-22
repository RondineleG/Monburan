namespace Monburan.Core.Interfaces;

/// <summary>Perfil que carrega o nome do plano vigente (empresa e candidato).</summary>
public interface IHasActivePlan
{
    string? ActivePlan { get; set; }
}
