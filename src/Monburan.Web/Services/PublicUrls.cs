namespace Monburan.Web.Services;

public static class PublicUrls
{
    /// <summary>
    /// URL pública absoluta de um caminho do site. Atrás de proxy TLS sem ForwardedHeaders, esquema e host da
    /// requisição seriam os internos (http://...), então vale <c>App:PublicBaseUrl</c> quando configurada
    /// (mesma regra dos links de e-mail).
    /// </summary>
    public static string Absolute(IConfiguration configuration, string requestBaseUri, string path)
    {
        var configured = configuration["App:PublicBaseUrl"];
        var baseUrl = !string.IsNullOrWhiteSpace(configured) ? configured : requestBaseUri;
        return baseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }
}
