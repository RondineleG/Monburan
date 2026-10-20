namespace Monburan.Web.Components.Account;

/// <summary>
/// Destino dos redirecionamentos do login, do cadastro e da verificação em duas etapas (o ReturnUrl vem da URL e qualquer um
/// monta esse link): só caminhos deste site.
/// </summary>
public static class SafeReturnUrl
{
    /// <summary>
    /// Caminho relativo ao site para <paramref name="uri"/>, ou "" (a página inicial) quando ele aponta para fora. "//host" e
    /// "/\host" passam como relativos no .NET, mas o navegador os resolve para outro host; um endereço absoluto de outro host
    /// fazia o ToBaseRelativePath lançar exceção (erro 500 depois de o login já ter acontecido).
    /// </summary>
    public static string ToSitePath(string? uri, string baseUri)
    {
        uri ??= "";
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            // Endereço absoluto: só o deste site (o RedirectToCurrentPage usa assim)
            if (!uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
                return "";
            uri = uri[baseUri.Length..];
        }

        return uri.StartsWith("//", StringComparison.Ordinal) || uri.StartsWith("/\\", StringComparison.Ordinal) || uri.StartsWith('\\')
            ? ""
            : uri;
    }
}
