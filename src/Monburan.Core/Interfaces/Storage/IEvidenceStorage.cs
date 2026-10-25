namespace Monburan.Core.Interfaces.Storage;

/// <summary>
/// Armazenamento privado das evidências de sugestões. Os arquivos não são servidos como estáticos: só o moderador
/// lê, por um caminho autorizado.
/// </summary>
public interface IEvidenceStorage
{
    /// <summary>Grava o conteúdo e devolve a chave interna do arquivo.</summary>
    Task<string> SaveAsync(byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
