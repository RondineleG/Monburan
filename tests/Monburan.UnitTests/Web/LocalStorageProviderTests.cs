using FluentAssertions;
using NSubstitute;

using Microsoft.AspNetCore.Hosting;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class LocalStorageProviderTests : IDisposable
{
    // URLs que escapam da pasta de uploads (path traversal)
    public static TheoryData<string> TraversalUrls => new()
    {
        "/uploads/../../appsettings.json",
        "/uploads//etc/passwd",
        "/uploads/../../../../etc/passwd"
    };

    private readonly string _webRoot;
    private readonly LocalStorageProvider _provider;

    public LocalStorageProviderTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "monburan-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.WebRootPath.Returns(_webRoot);

        _provider = new LocalStorageProvider(environment);
    }

    [Theory]
    [MemberData(nameof(TraversalUrls))]
    public async Task DeleteAsync_WhenUrlEscapesUploadsFolder_ReturnsFalse(string fileUrl)
    {
        var secretFile = Path.Combine(_webRoot, "appsettings.json");
        await File.WriteAllTextAsync(secretFile, "conteudo secreto");

        var deleted = await _provider.DeleteAsync(fileUrl);

        deleted.Should().BeFalse();
        // O arquivo fora da pasta de uploads continua intacto
        File.Exists(secretFile).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(TraversalUrls))]
    public async Task DownloadAsync_WhenUrlEscapesUploadsFolder_ThrowsArgumentException(string fileUrl)
    {
        var act = async () => await _provider.DownloadAsync(fileUrl);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenFileIsInsideUploadsFolder_DeletesFile()
    {
        var filePath = GivenUploadedFile("2026", "10", "contrato.pdf");

        var deleted = await _provider.DeleteAsync("/uploads/2026/10/contrato.pdf");

        deleted.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAsync_WhenFileIsInsideUploadsFolder_ReturnsFileContent()
    {
        GivenUploadedFile("2026", "10", "contrato.pdf", "conteudo do contrato");

        var stream = await _provider.DownloadAsync("/uploads/2026/10/contrato.pdf");

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            (await reader.ReadToEndAsync()).Should().Be("conteudo do contrato");
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenFileDoesNotExist_ThrowsFileNotFoundExceptionWithFileNameOnly()
    {
        var act = async () => await _provider.DownloadAsync("/uploads/2026/10/inexistente.pdf");

        var exception = (await act.Should().ThrowAsync<FileNotFoundException>()).Which;

        // A mensagem não pode expor o caminho completo do servidor
        exception.Message.Should().Contain("inexistente.pdf");
        exception.Message.Should().NotContain(_webRoot);
    }

    [Fact]
    public async Task DeleteAsync_WhenUrlIsNotAnUploadUrl_ReturnsFalse()
    {
        var deleted = await _provider.DeleteAsync("uploadsQualquerCoisa/2026/10/contrato.pdf");

        deleted.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, true);

        GC.SuppressFinalize(this);
    }

    private string GivenUploadedFile(string year, string month, string fileName, string content = "conteudo")
    {
        var folder = Path.Combine(_webRoot, "uploads", year, month);
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, fileName);
        File.WriteAllText(filePath, content);

        return filePath;
    }
}
