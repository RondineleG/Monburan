using System.Text;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.UseCases.Storage;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Storage;

public class UploadFileUseCaseTests
{
    private readonly IStorageProvider _storage = Substitute.For<IStorageProvider>();
    private readonly UploadFileUseCase _useCase;

    public UploadFileUseCaseTests()
    {
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => $"/uploads/2026/10/arquivo{ExtensionFor(call.ArgAt<string>(2))}");
        _useCase = new UploadFileUseCase(_storage);
    }

    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public void DetectContentType_RecognizesSupportedSignatures(byte[] header, string expected)
    {
        UploadFileUseCase.DetectContentType(header).Should().Be(expected);
    }

    [Fact]
    public void DetectContentType_ReturnsNull_ForHtml()
    {
        UploadFileUseCase.DetectContentType("<html>"u8).Should().BeNull();
    }

    [Fact]
    public async Task Execute_RejectsHtmlDisguisedAsPdf()
    {
        var result = await _useCase.ExecuteAsync(Request("<html><script>alert(1)</script>", "application/pdf", EFileType.Pdf));

        result.Status.Should().NotBe(EResultStatus.Success);
        result.ValidationResult.Errors.Should().ContainSingle(e => e.Message == "O arquivo não é um PDF válido.");
        await _storage.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_UsesTheDetectedContentType()
    {
        // Celulares às vezes informam "image/png" para fotos JPEG — o tipo real prevalece
        var jpeg = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        var result = await _useCase.ExecuteAsync(new UploadFileRequest
        {
            FileStream = jpeg,
            FileName = "foto.png",
            ContentType = "image/png",
            FileType = EFileType.Image
        });

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ContentType.Should().Be("image/jpeg");
        await _storage.Received(1).UploadAsync(Arg.Any<Stream>(), "foto.png", "image/jpeg");
    }

    [Fact]
    public async Task Execute_AcceptsNonSeekableStreams()
    {
        var result = await _useCase.ExecuteAsync(new UploadFileRequest
        {
            FileStream = new NonSeekableStream(Encoding.ASCII.GetBytes("%PDF-1.4 conteudo")),
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            FileType = EFileType.Pdf
        });

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.FileUrl.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task Execute_AcceptsNonSeekableStreamWhoseLengthIsUnknown()
    {
        // O stream do Blazor (IBrowserFile.OpenReadStream) não aceita seek E não sabe o próprio
        // tamanho: Length lança NotSupportedException. Medir o tamanho antes de bufferizar
        // quebrava o upload de logo e de currículo com 500.
        var result = await _useCase.ExecuteAsync(new UploadFileRequest
        {
            FileStream = new NonSeekableStream(Encoding.ASCII.GetBytes("%PDF-1.4 conteudo"), lengthIsUnknown: true),
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            FileType = EFileType.Pdf
        });

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.FileSize.Should().Be(Encoding.ASCII.GetBytes("%PDF-1.4 conteudo").Length);
        await _storage.Received(1).UploadAsync(Arg.Any<Stream>(), "curriculo.pdf", "application/pdf");
    }

    [Fact]
    public async Task Execute_RejectsNonSeekableStreamBiggerThanTheLimit()
    {
        // Um arquivo grande demais é recusado sem carregar o arquivo inteiro na memória
        var big = new byte[6 * 1024 * 1024];
        big[0] = 0x89; big[1] = 0x50; big[2] = 0x4E; big[3] = 0x47;

        var result = await _useCase.ExecuteAsync(new UploadFileRequest
        {
            FileStream = new NonSeekableStream(big, lengthIsUnknown: true),
            FileName = "foto.png",
            ContentType = "image/png",
            FileType = EFileType.Image
        });

        result.Status.Should().NotBe(EResultStatus.Success);
        result.ValidationResult.Errors.Should().ContainSingle(e => e.Message.Contains("excede o limite de 5MB"));
        await _storage.DidNotReceive().UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>());
    }

    private static UploadFileRequest Request(string content, string contentType, EFileType type) => new()
    {
        FileStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        FileName = "arquivo",
        ContentType = contentType,
        FileType = type
    };

    private static string ExtensionFor(string contentType) => contentType == "application/pdf" ? ".pdf" : ".img";

    /// <summary>
    /// Simula o stream de upload do Blazor (sem seek). Com <c>lengthIsUnknown</c> também
    /// simula o <c>BrowserFileStream</c> real, que lança ao tentar informar o próprio tamanho.
    /// </summary>
    private sealed class NonSeekableStream(byte[] data, bool lengthIsUnknown = false) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => lengthIsUnknown ? throw new NotSupportedException() : _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
