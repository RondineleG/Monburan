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
        {
            var errorResult = RequestResult<UploadFileResponse>.WithNoContent();
            foreach (var error in validationResult.ValidationResult.Errors)
            {
                errorResult.ValidationResult.AddError(error.Message, error.Field);
            }
            return errorResult;
        }

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
