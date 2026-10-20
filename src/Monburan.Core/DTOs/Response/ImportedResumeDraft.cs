using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Response;

/// <summary>
/// Currículo lido de um PDF ainda não confirmado pela pessoa. Traz os dados estruturados no formato
/// do formulário, de onde vieram (<see cref="ExtractedBy"/>) e o que ficou faltando ou incerto
/// (<see cref="MissingFields"/>) para o questionário perguntar só isso.
/// </summary>
public class ImportedResumeDraft
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public List<ImportedExperience> Experiences { get; set; } = [];
    public List<ImportedEducation> Education { get; set; } = [];
    public List<ImportedLanguage> Languages { get; set; } = [];
    public List<ImportedSkill> Skills { get; set; } = [];

    /// <summary>Texto bruto extraído do PDF, já limitado em tamanho. Útil para depuração e reprocessamento.</summary>
    public string? RawText { get; set; }

    /// <summary>Como o rascunho foi produzido.</summary>
    public EResumeImportMode ExtractedBy { get; set; } = EResumeImportMode.None;

    /// <summary>Nome do campo ainda vazio ou incerto, na ordem em que o wizard deve perguntar.</summary>
    public List<string> MissingFields { get; set; } = [];
}

public class ImportedExperience
{
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class ImportedEducation
{
    public string Institution { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public EEducationLevel Level { get; set; } = EEducationLevel.Bachelor;
    public EEducationStatus Status { get; set; } = EEducationStatus.Completed;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ImportedLanguage
{
    public string Language { get; set; } = string.Empty;
    public ELanguageLevel Level { get; set; } = ELanguageLevel.Intermediate;
}

public class ImportedSkill
{
    public string Skill { get; set; } = string.Empty;
    public int Level { get; set; } = 3;
    public int YearsOfExperience { get; set; }
}

/// <summary>De onde veio a leitura do PDF.</summary>
public enum EResumeImportMode
{
    /// <summary>Nada extraído: PDF sem camada de texto ou leitura falhou.</summary>
    None,
    /// <summary>Leitura local por regras, sem enviar o conteúdo a nenhum serviço externo.</summary>
    Local,
    /// <summary>Estruturado pelo provedor de IA, após autorização expressa da pessoa.</summary>
    Ai
}
