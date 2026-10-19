namespace Monburan.Core.DTOs.Response;

/// <summary>Sugestões geradas por IA para um currículo. Nada é gravado: a pessoa decide o que aproveitar.</summary>
public class ResumeImprovementResponse
{
    /// <summary>Resumo profissional reescrito (vazio quando o currículo não dá base para escrever um).</summary>
    public string Summary { get; set; } = string.Empty;

    public List<ExperienceImprovement> Experiences { get; set; } = [];

    public List<string> SkillsToAdd { get; set; } = [];

    public List<string> MissingSections { get; set; } = [];

    public List<string> Tips { get; set; } = [];

    /// <summary>Modelo que gerou o texto, por transparência.</summary>
    public string? Model { get; set; }

    public DateTime GeneratedAt { get; set; }
}

public class ExperienceImprovement
{
    public string Company { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public List<string> Suggestions { get; set; } = [];
}
