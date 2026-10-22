using System.Text;

using Monburan.Core.Interfaces.Services;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace Monburan.Core.Services;

/// <summary>
/// Lê o texto de cada página com PdfPig. A biblioteca falha com exceções próprias em arquivos
/// corrompidos ou protegidos; aqui tudo vira <see cref="PdfExtractionException"/> para a camada
/// de cima não depender do detalhe.
/// </summary>
public class PdfTextExtractor : IPdfTextExtractor
{
    public string ExtractText(Stream pdfStream)
    {
        try
        {
            var text = new StringBuilder();
            using var document = PdfDocument.Open(pdfStream);
            foreach (var page in document.GetPages())
            {
                var content = page.Text;
                if (!string.IsNullOrWhiteSpace(content))
                    text.AppendLine(content);
            }

            return text.ToString().Trim();
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new PdfExtractionException("O PDF está protegido por senha e não pôde ser lido.", exception);
        }
        catch (Exception exception) when (exception is not PdfExtractionException)
        {
            throw new PdfExtractionException("O arquivo está corrompido ou não é um PDF válido.", exception);
        }
    }
}
