namespace Monburan.Core.DTOs.Response;

/// <summary>Resultado da importação de um PDF: o currículo criado, como foi lido e o que falta perguntar.</summary>
public class ImportResumeResult
{
    /// <summary>Currículo criado com os dados extraídos (pode estar quase vazio se nada foi lido).</summary>
    public Guid ResumeId { get; set; }

    /// <summary>Como o rascunho foi produzido: IA, regras locais ou nada.</summary>
    public EResumeImportMode ExtractedBy { get; set; }

    /// <summary>Campos ainda vazios ou incertos, na ordem das perguntas do wizard.</summary>
    public List<string> MissingFields { get; set; } = [];

    /// <summary>Quantas seções vieram preenchidas (para a tela resumir o que foi encontrado).</summary>
    public int SectionsFound { get; set; }
}
