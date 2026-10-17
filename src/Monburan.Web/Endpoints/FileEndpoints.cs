using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.UseCases.Storage;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class FileEndpoints
{
    public static RouteGroupBuilder MapFileEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/files").WithTags("Files")
            .RequireAuthorization("ApiBearer");

        // Multipart upload — bearer API não tem antiforgery token
        group.MapPost("/upload", async (
            IFormFile file,
            EFileType fileType,
            UploadFileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Arquivo é obrigatório"]
                }, title: "Erro de validação");
            }

            // Apenas imagens e PDF são servidos de wwwroot/uploads — tipos arbitrários
            // (html/svg/js) abririam stored XSS na origem do site
            if (fileType is not (EFileType.Image or EFileType.Pdf))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["fileType"] = ["Tipo de arquivo não suportado. Use Image ou Pdf."]
                }, title: "Erro de validação");
            }

            await using var stream = file.OpenReadStream();
            var request = new UploadFileRequest
            {
                FileStream = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileType = fileType
            };

            var result = await useCase.ExecuteAsync(request);
            return result.ToHttpResult();
        })
        .DisableAntiforgery()
        .WithSummary("Upload de arquivo (imagem ≤5MB, PDF ≤10MB). Retorna FileUrl relativa");

        return api;
    }
}
