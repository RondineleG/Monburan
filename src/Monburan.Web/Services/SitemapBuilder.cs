using System.Net;
using System.Text;

namespace Monburan.Web.Services;

/// <summary>Uma URL pública do sitemap, com a última modificação quando conhecida.</summary>
public sealed record SitemapUrl(string Loc, DateTime? LastModified);

/// <summary>Monta o sitemap.xml (schema sitemaps.org) do site. Regra pura, testada em unitário.</summary>
public static class SitemapBuilder
{
    public static string Build(IEnumerable<SitemapUrl> urls)
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
        xml.Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');

        foreach (var url in urls)
        {
            xml.Append("  <url><loc>").Append(WebUtility.HtmlEncode(url.Loc)).Append("</loc>");
            if (url.LastModified is { } lastModified)
                xml.Append("<lastmod>").Append(lastModified.ToString("yyyy-MM-dd")).Append("</lastmod>");
            xml.Append("</url>\n");
        }

        xml.Append("</urlset>");
        return xml.ToString();
    }
}
