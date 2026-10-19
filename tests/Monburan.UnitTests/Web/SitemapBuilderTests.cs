using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class SitemapBuilderTests
{
    [Fact]
    public void Build_WritesTheUrlSetWithLocAndLastMod()
    {
        var xml = SitemapBuilder.Build(
        [
            new SitemapUrl("https://monburan.com.br/", null),
            new SitemapUrl("https://monburan.com.br/jobs", new DateTime(2026, 10, 19, 15, 30, 0, DateTimeKind.Utc))
        ]);

        xml.Should().StartWith("""<?xml version="1.0" encoding="UTF-8"?>""");
        xml.Should().Contain("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");
        xml.Should().Contain("<url><loc>https://monburan.com.br/</loc></url>");
        xml.Should().Contain("<url><loc>https://monburan.com.br/jobs</loc><lastmod>2026-10-19</lastmod></url>");
        xml.Should().EndWith("</urlset>");
    }

    [Fact]
    public void Build_EscapesTheUrls()
    {
        var xml = SitemapBuilder.Build([new SitemapUrl("https://monburan.com.br/jobs?c=1&tipo=\"x\"", null)]);

        xml.Should().Contain("https://monburan.com.br/jobs?c=1&amp;tipo=&quot;x&quot;");
        xml.Should().NotContain("c=1&tipo");
    }

    [Fact]
    public void Build_WithoutUrls_StillWritesAValidUrlSet()
    {
        var xml = SitemapBuilder.Build([]);

        xml.Should().Contain("<urlset");
        xml.Should().EndWith("</urlset>");
    }
}
