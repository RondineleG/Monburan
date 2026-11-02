using System.Net;
using System.Text.RegularExpressions;

using FluentAssertions;

namespace Monburan.ApiTests;

/// <summary>O que o site de fato entrega: a política do CSP e páginas sem script nem manipulador inline que ela bloquearia.</summary>
public partial class ContentSecurityPolicyApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public ContentSecurityPolicyApiTests(MonburanApiFactory factory) => _client = factory.CreateClient();

    public static TheoryData<string> Pages() => new() { "/", "/jobs", "/entrar", "/criar-conta", "/privacidade", "/plans" };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task EveryPage_ShipsTheStrictPolicy(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        policy.Should().Contain("script-src 'self';");
        policy.Should().NotContain("unsafe-eval");
        policy.Should().Contain("frame-ancestors 'none'");
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task EveryPage_HasNoInlineScriptAndNoInlineEventHandler(string path)
    {
        var html = await (await _client.GetAsync(path)).Content.ReadAsStringAsync();

        // <script> sem src só é aceito quando é bloco de dados (JSON-LD), que o CSP não executa
        InlineScript().Matches(html).Select(match => match.Value).Should().BeEmpty($"{path} não pode ter script inline");
        InlineHandler().Matches(html).Select(match => match.Value).Should().BeEmpty($"{path} não pode ter onclick= e similares");
        html.Should().NotContain("javascript:", $"{path} não pode ter links javascript:");
    }

    [Theory]
    [InlineData("/theme-init.js")]
    [InlineData("/app-shell.js")]
    [InlineData("/monburan-ui.js")]
    public async Task TheScriptsMovedOutOfThePage_AreServed(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("javascript");
        (await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ThePageLoadsTheMovedScripts_WithPathsThatWorkFromAnyRoute()
    {
        var html = await (await _client.GetAsync("/jobs")).Content.ReadAsStringAsync();

        html.Should().Contain("src=\"/theme-init.js");
        html.Should().Contain("app-shell.js");
    }

    [GeneratedRegex(@"<script(?![^>]*\bsrc\s*=)(?![^>]*type\s*=\s*""application/(ld\+)?json"")[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"<[a-z][^>]*\son[a-z]+\s*=\s*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();
}
