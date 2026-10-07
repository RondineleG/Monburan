namespace Monburan.Core.DTOs.Response;

/// <summary>
/// Resposta de upload de arquivo.
/// </summary>
public class UploadFileResponse
{
    /// <summary>
    /// URL do arquivo armazenado.
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// Nome original do arquivo.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do arquivo em bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Content-Type do arquivo.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}
