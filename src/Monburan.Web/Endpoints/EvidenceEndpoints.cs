using Microsoft.EntityFrameworkCore;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Entrega a imagem de uma evidência para a moderação. A tela de moderação mandava as imagens em base64 pelo SignalR
/// (cerca de 33% maiores, em UTF-16 na memória do circuito, até 15 MB por sugestão); agora o navegador baixa por HTTP,
/// em fluxo e com cache, sem passar pelo circuito.
/// </summary>
public static class EvidenceEndpoints
{
    public const string RoutePrefix = "/admin/sugestoes/evidencias";

    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        // Mesma política da tela de moderação (papel Admin + verificação em duas etapas). O arquivo continua privado:
        // sem sessão de administrador não há como baixá-lo
        app.MapGet(RoutePrefix + "/{id:guid}", GetAsync)
            .RequireAuthorization("AdminOnly")
            .ExcludeFromDescription();

        return app;
    }

    public static string UrlFor(Guid evidenceId) => $"{RoutePrefix}/{evidenceId:D}";

    private static async Task<IResult> GetAsync(
        Guid id,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IEvidenceStorage storage,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var evidenceId = Id.Create(id);
        var evidence = await context.JobSubmissionEvidences.AsNoTracking()
            .Where(e => e.Id == evidenceId)
            .Select(e => new { e.StorageKey, e.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        var stream = evidence is null ? null : storage.OpenRead(evidence.StorageKey);
        if (evidence is null || stream is null)
            return Results.NotFound();

        // A chave do arquivo é única e o conteúdo nunca muda: o navegador do moderador pode guardar a imagem. "private"
        // impede que um proxy ou CDN guarde uma imagem que só o administrador pode ver
        http.Response.Headers.CacheControl = "private, max-age=3600";
        return Results.Stream(stream, evidence.ContentType);
    }
}
