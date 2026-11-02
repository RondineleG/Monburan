namespace Monburan.Core.DTOs.Response;

/// <summary>Experiência exibida no link público (somente os campos que o candidato liberou).</summary>
public sealed record PublicExperience(string Position, string Company, DateTime StartDate, DateTime? EndDate, bool IsCurrent, string? Description);

/// <summary>Formação exibida no link público.</summary>
public sealed record PublicEducation(string Course, string Institution, string Level, string Status, DateTime? StartDate, DateTime? EndDate);

/// <summary>Projeção pública do currículo. Nunca contém o identificador interno nem o arquivo original.</summary>
public sealed class PublicResumeView
{
    public string Title { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Summary { get; set; }

    public IReadOnlyList<PublicExperience> Experiences { get; set; } = [];

    public IReadOnlyList<PublicEducation> Education { get; set; } = [];

    public IReadOnlyList<string> Skills { get; set; } = [];

    public IReadOnlyList<string> Languages { get; set; } = [];
}
