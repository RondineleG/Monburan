namespace Monburan.Core.DTOs.Request;

/// <summary>
/// Request para upload de arquivos.
/// </summary>
public class UploadFileRequest
{
    /// <summary>
    /// Stream do arquivo para upload.
    /// </summary>
    public Stream FileStream { get; set; } = Stream.Null;

    /// <summary>
    /// Nome original do arquivo.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Content-Type do arquivo (ex: image/jpeg, application/pdf).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de arquivo para validação de tamanho (Image, Pdf, Other).
    /// </summary>
    public EFileType FileType { get; set; } = EFileType.Other;
}

/// <summary>
/// Tipos de arquivo suportados para validação.
/// </summary>
public enum EFileType
{
    Image,
    Pdf,
    Other
}
