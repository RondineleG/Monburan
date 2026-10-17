using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Storage;

/// <summary>
/// Use case para upload de arquivos com validação de tamanho e tipo.
/// </summary>
public class UploadFileUseCase : IUseCase<UploadFileRequest, UploadFileResponse>
{
    private readonly IStorageProvider _storageProvider;

    // Tamanhos máximos em bytes
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    private const long MaxPdfSize = 10 * 1024 * 1024; // 10MB
    private const long MaxOtherSize = 10 * 1024 * 1024; // 10MB

    // Content-Types permitidos
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedPdfTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf"
    };

    public UploadFileUseCase(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    public async Task<RequestResult<UploadFileResponse>> ExecuteAsync(UploadFileRequest request)
    {
        // Validar request
        var validationResult = ValidateRequest(request);
        if (validationResult.Status != EResultStatus.Success)
            return ToValidationFailure(validationResult);

        // Streams sem seek (ex.: upload do Blazor) são bufferizados para ler o cabeçalho
        // e depois gravar o arquivo inteiro — o tamanho já foi limitado acima.
        await using var buffered = await EnsureSeekableAsync(request);

        var signatureResult = await ValidateSignatureAsync(request);
        if (signatureResult.Status != EResultStatus.Success)
            return ToValidationFailure(signatureResult);

        try
        {
            // Realizar upload
            var fileUrl = await _storageProvider.UploadAsync(
                request.FileStream,
                request.FileName,
                request.ContentType);

            // Calcular tamanho do arquivo (resetar posição do stream)
            var fileSize = request.FileStream.Length;

            var response = new UploadFileResponse
            {
                FileUrl = fileUrl,
                OriginalFileName = request.FileName,
                FileSize = fileSize,
                ContentType = request.ContentType
            };

            return RequestResult<UploadFileResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return RequestResult<UploadFileResponse>.WithError(ex);
        }
    }

    private static RequestResult<UploadFileResponse> ToValidationFailure(RequestResult validation)
    {
        var errorResult = RequestResult<UploadFileResponse>.WithNoContent();
        foreach (var error in validation.ValidationResult.Errors)
        {
            errorResult.ValidationResult.AddError(error.Message, error.Field);
        }
        return errorResult;
    }

    private static async Task<MemoryStream?> EnsureSeekableAsync(UploadFileRequest request)
    {
        if (request.FileStream.CanSeek)
            return null;

        var buffer = new MemoryStream();
        await request.FileStream.CopyToAsync(buffer);
        buffer.Position = 0;
        request.FileStream = buffer;
        return buffer;
    }

    /// <summary>
    /// Confere a assinatura (magic bytes) do arquivo: o content-type vem do cliente e pode
    /// ser forjado (ex.: HTML enviado como image/png). O tipo detectado passa a ser o oficial.
    /// </summary>
    private static async Task<RequestResult> ValidateSignatureAsync(UploadFileRequest request)
    {
        if (request.FileType is not (EFileType.Image or EFileType.Pdf))
            return RequestResult.Success();

        var header = new byte[16];
        var start = request.FileStream.Position;
        var read = await request.FileStream.ReadAsync(header);
        request.FileStream.Position = start;

        var detected = DetectContentType(header.AsSpan(0, read));
        if (detected is null || !IsContentTypeAllowed(detected, request.FileType))
        {
            return RequestResult.WithValidationError(
                request.FileType == EFileType.Pdf
                    ? "O arquivo não é um PDF válido."
                    : "O arquivo não é uma imagem válida. Envie JPEG, PNG, GIF ou WebP.",
                nameof(request.FileStream));
        }

        request.ContentType = detected;
        return RequestResult.Success();
    }

    /// <summary>Content-type real pelo cabeçalho do arquivo, ou <c>null</c> se não reconhecido.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("%PDF"u8))
            return "application/pdf";
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]))
            return "image/png";
        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
            return "image/gif";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }

    private static RequestResult ValidateRequest(UploadFileRequest request)
    {
        // Validar nome do arquivo
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return RequestResult.WithValidationError("Nome do arquivo é obrigatório.", nameof(request.FileName));
        }

        // Validar content-type
        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return RequestResult.WithValidationError("Content-Type do arquivo é obrigatório.", nameof(request.ContentType));
        }

        // Validar tipo de arquivo e tamanho
        var maxSize = GetMaxSize(request.FileType);
        if (request.FileStream.Length > maxSize)
        {
            var maxSizeMb = maxSize / (1024 * 1024);
            return RequestResult.WithValidationError(
                $"Tamanho do arquivo excede o limite de {maxSizeMb}MB.",
                nameof(request.FileStream));
        }

        // Validar content-type permitido
        if (!IsContentTypeAllowed(request.ContentType, request.FileType))
        {
            return RequestResult.WithValidationError(
                "Tipo de arquivo não permitido. Tipos aceitos: JPEG, PNG, GIF, WebP, PDF.",
                nameof(request.ContentType));
        }

        return RequestResult.Success();
    }

    private static long GetMaxSize(EFileType fileType)
    {
        return fileType switch
        {
            EFileType.Image => MaxImageSize,
            EFileType.Pdf => MaxPdfSize,
            _ => MaxOtherSize
        };
    }

    private static bool IsContentTypeAllowed(string contentType, EFileType fileType)
    {
        return fileType switch
        {
            EFileType.Image => AllowedImageTypes.Contains(contentType),
            EFileType.Pdf => AllowedPdfTypes.Contains(contentType),
            _ => true // Outros tipos são permitidos (validação de tamanho já feita)
        };
    }
}
