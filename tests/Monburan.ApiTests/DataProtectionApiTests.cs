using FluentAssertions;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Monburan.ApiTests;

/// <summary>A mesma API com as chaves do DataProtection numa pasta (como o volume do contêiner em produção).</summary>
public class PersistedKeysApiFactory(string keysPath) : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:KeysPath"] = keysPath }));
    }
}

/// <summary>O que foi protegido antes de um deploy (cookie, token de e-mail) continua válido depois dele.</summary>
public sealed class DataProtectionApiTests : IDisposable
{
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "monburan-keys-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_keysPath))
            Directory.Delete(_keysPath, recursive: true);
    }

    private static string Protect(MonburanApiFactory factory, string text) =>
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("teste-de-deploy").Protect(text);

    private static string Unprotect(MonburanApiFactory factory, string payload) =>
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("teste-de-deploy").Unprotect(payload);

    [Fact]
    public void TheKeysSurviveARestart_SoADeployDoesNotSignEveryoneOut()
    {
        string payload;
        using (var first = new PersistedKeysApiFactory(_keysPath))
        {
            payload = Protect(first, "sessão de antes do deploy");
        }

        Directory.GetFiles(_keysPath, "key-*.xml").Should().NotBeEmpty("a chave precisa ter ido para a pasta persistente");

        using var second = new PersistedKeysApiFactory(_keysPath);
        Unprotect(second, payload).Should().Be("sessão de antes do deploy");
    }
}
