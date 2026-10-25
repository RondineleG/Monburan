using Microsoft.Extensions.Configuration;
using Monburan.Core.Interfaces.Storage;

namespace Monburan.Web.Services;

/// <summary>
/// Evidências de sugestões em uma pasta privada (App_Data, fora de wwwroot). Nada é servido como estático: a leitura
/// só acontece pela moderação. Os nomes são gerados pelo sistema e o caminho é conferido para não sair da pasta.
/// </summary>
public sealed class LocalEvidenceStorage : IEvidenceStorage
{
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly string _root;

    public LocalEvidenceStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Evidence:StoragePath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "evidencias")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!Extensions.TryGetValue(contentType, out var extension))
            throw new InvalidOperationException("Tipo de evidência não suportado.");

        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return key;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de evidência inválido.");
        return path;
    }
}
