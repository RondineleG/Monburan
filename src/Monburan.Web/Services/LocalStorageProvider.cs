using Microsoft.AspNetCore.Hosting;

namespace Monburan.Web.Services;

/// <summary>
/// Implementação de IStorageProvider que armazena arquivos localmente em wwwroot/uploads/.
/// Gera nomes únicos para evitar colisões e organiza por subpastas mensais.
/// </summary>
public sealed class LocalStorageProvider : Core.Interfaces.Storage.IStorageProvider
{
    private readonly string _uploadsPath;
    private readonly string _baseUrl;

    // Prefixo das URLs de upload ("/uploads/..."): o caminho do arquivo vem depois dele
    private const string UploadsPrefix = "uploads/";

    // Extensão é derivada do content-type validado, nunca do filename do cliente —
    // impede que um "evil.html" enviado como image/png vire arquivo executável no site.
    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["application/pdf"] = ".pdf"
    };

    public LocalStorageProvider(IWebHostEnvironment environment)
    {
        // Caminho canônico da raiz de uploads: toda URL resolvida é conferida
        // contra ele para impedir que o arquivo fique fora da pasta (path traversal).
        _uploadsPath = Path.GetFullPath(Path.Combine(environment.WebRootPath, "uploads"));
        _baseUrl = "/uploads";

        // Garantir que a pasta de uploads existe
        if (!Directory.Exists(_uploadsPath))
        {
            Directory.CreateDirectory(_uploadsPath);
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        // Gerar nome único com GUID + extensão canônica do content-type
        var extension = ExtensionByContentType.GetValueOrDefault(contentType ?? string.Empty, ".bin");
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";

        // Organizar por subpasta mensal (ex: uploads/2024/01/)
        var year = DateTime.Now.Year.ToString();
        var month = DateTime.Now.Month.ToString("D2");
        var folderPath = Path.Combine(_uploadsPath, year, month);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var filePath = Path.Combine(folderPath, uniqueFileName);

        // Salvar arquivo
        await using var fileStreamOut = File.Create(filePath);
        await fileStream.CopyToAsync(fileStreamOut);

        // Retornar URL relativa
        var relativeUrl = $"{_baseUrl}/{year}/{month}/{uniqueFileName}";
        return relativeUrl;
    }

    public Task<bool> DeleteAsync(string fileUrl)
    {
        if (!TryResolveUploadPath(fileUrl, out var filePath))
            return Task.FromResult(false);

        if (!File.Exists(filePath))
            return Task.FromResult(false);

        try
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<Stream> DownloadAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("URL do arquivo é obrigatória.", nameof(fileUrl));

        if (!TryResolveUploadPath(fileUrl, out var filePath))
            throw new ArgumentException("URL inválida. Deve ser uma URL de upload.", nameof(fileUrl));

        if (!File.Exists(filePath))
            // Só o nome do arquivo: o caminho completo revelaria a estrutura do servidor
            throw new FileNotFoundException($"Arquivo não encontrado: {Path.GetFileName(filePath)}");

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }

    /// <summary>
    /// Converte a URL de upload em caminho físico, garantindo que o resultado
    /// continue dentro da pasta de uploads.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.Combine"/> sozinho não protege: "uploads/../../appsettings.json"
    /// sai da pasta e "uploads//etc/passwd" vira caminho absoluto (o <c>Combine</c>
    /// descarta a base). Por isso o caminho é canonicalizado com
    /// <see cref="Path.GetFullPath(string)"/> e conferido contra a raiz.
    /// </remarks>
    private bool TryResolveUploadPath(string fileUrl, out string filePath)
    {
        filePath = string.Empty;

        if (string.IsNullOrWhiteSpace(fileUrl))
            return false;

        // Ex: /uploads/2024/01/abc123.jpg -> uploads/2024/01/abc123.jpg
        var relativePath = fileUrl.TrimStart('/');

        // Só aceita URL dentro de /uploads/ (com barra): "uploadsQualquer" não é URL de upload
        if (!relativePath.StartsWith(UploadsPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var relativeFilePath = relativePath[UploadsPrefix.Length..];
        if (relativeFilePath.Length == 0)
            return false;

        // GetFullPath resolve "..", "." e caminhos absolutos
        var resolvedPath = Path.GetFullPath(Path.Combine(_uploadsPath, relativeFilePath));

        // O caminho resolvido precisa continuar dentro da raiz de uploads
        var rootWithSeparator = _uploadsPath.EndsWith(Path.DirectorySeparatorChar)
            ? _uploadsPath
            : _uploadsPath + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            return false;

        filePath = resolvedPath;
        return true;
    }
}
