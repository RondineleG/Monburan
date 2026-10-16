using Microsoft.AspNetCore.Hosting;

using System.Security.Cryptography;

namespace Monburan.Web.Services;

/// <summary>
/// Implementação de IStorageProvider que armazena arquivos localmente em wwwroot/uploads/.
/// Gera nomes únicos para evitar colisões e organiza por subpastas mensais.
/// </summary>
public sealed class LocalStorageProvider : Core.Interfaces.Storage.IStorageProvider
{
    private readonly string _uploadsPath;
    private readonly string _baseUrl;

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
        _uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
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
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.FromResult(false);

        // Converter URL relativa para caminho físico
        // Ex: /uploads/2024/01/abc123.jpg -> uploads/2024/01/abc123.jpg
        var relativePath = fileUrl.TrimStart('/');
        if (!relativePath.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        var filePath = Path.Combine(_uploadsPath, relativePath["uploads/".Length..]);

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

        var relativePath = fileUrl.TrimStart('/');
        if (!relativePath.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("URL inválida. Deve ser uma URL de upload.", nameof(fileUrl));

        var filePath = Path.Combine(_uploadsPath, relativePath["uploads/".Length..]);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Arquivo não encontrado: {fileUrl}");

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }
}
