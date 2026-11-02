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

    /// <summary>
    /// Abre o arquivo para leitura em fluxo (sem carregar tudo na memória), ou null se ele não existe. Quem chama
    /// descarta o fluxo.
    /// </summary>
    Stream? OpenRead(string storageKey);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
