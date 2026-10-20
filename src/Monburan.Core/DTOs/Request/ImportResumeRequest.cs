namespace Monburan.Core.DTOs.Request;

/// <summary>Pedido de leitura de um PDF de currículo já enviado ao armazenamento.</summary>
public class ImportResumeRequest
{
    /// <summary>Conteúdo binário do PDF (o arquivo já foi validado como PDF no upload).</summary>
    public required Stream FileStream { get; set; }

    /// <summary>Nome original do arquivo, usado como título inicial do rascunho.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Onde o PDF ficou gravado após o upload (guardado como documento de origem do currículo).</summary>
    public string? FileUrl { get; set; }

    /// <summary>Candidato dono do currículo que será criado.</summary>
    public Guid CandidateId { get; set; }

    /// <summary>
    /// Autorização expressa para mandar o conteúdo do PDF ao provedor de IA externo.
    /// Sem ela a leitura é só local (regras), nunca sai conteúdo do servidor.
    /// </summary>
    public bool AcceptsExternalProcessing { get; set; }
}
