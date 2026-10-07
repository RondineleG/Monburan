namespace Monburan.Core.Interfaces.Storage;

/// <summary>
/// Abstração para provedores de armazenamento de arquivos.
/// Permite trocar entre storage local, Azure Blob, AWS S3, etc.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Faz upload de um arquivo e retorna a URL de acesso.
    /// </summary>
    /// <param name="fileStream">Stream do arquivo</param>
    /// <param name="fileName">Nome original do arquivo</param>
    /// <param name="contentType">Content-Type do arquivo</param>
    /// <returns>URL do arquivo armazenado</returns>
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Remove um arquivo pelo seu URL.
    /// </summary>
    /// <param name="fileUrl">URL do arquivo a ser removido</param>
    /// <returns>True se removido com sucesso, False se não encontrado</returns>
    Task<bool> DeleteAsync(string fileUrl);

    /// <summary>
    /// Retorna o stream de um arquivo armazenado.
    /// </summary>
    /// <param name="fileUrl">URL do arquivo</param>
    /// <returns>Stream do arquivo</returns>
    Task<Stream> DownloadAsync(string fileUrl);
}
