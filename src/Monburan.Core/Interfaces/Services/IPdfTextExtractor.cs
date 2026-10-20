namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Extrai o texto de um PDF de currículo. PDFs digitalizados (só imagem) não têm camada de texto:
/// nesse caso o resultado vem vazio e quem chamou decide como avisar a pessoa.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>Texto plano do PDF, página a página. Vazio quando o documento não tem camada de texto.</summary>
    /// <exception cref="PdfExtractionException">Arquivo corrompido, protegido ou que não é um PDF válido.</exception>
    string ExtractText(Stream pdfStream);
}

/// <summary>Falha prevista ao ler um PDF: arquivo inválido, corrompido ou protegido por senha.</summary>
public class PdfExtractionException : Exception
{
    public PdfExtractionException(string message, Exception? inner = null) : base(message, inner) { }
}
