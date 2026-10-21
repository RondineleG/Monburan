using System.Text.RegularExpressions;

using FluentAssertions;

namespace Monburan.UnitTests.Mobile;

/// <summary>
/// O app (MAUI Blazor) tem cópia própria dos tokens de cor. Cada cor declarada lá precisa ter o mesmo valor
/// da web (tokens.css), que é a fonte verificada por contraste: sem isso as duas interfaces voltam a divergir.
/// </summary>
public partial class MobileTokenParityTests
{
    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public void MobileColorTokens_MatchTheWebTokens(string theme)
    {
        var web = Block(Read("src", "Monburan.Web", "wwwroot", "tokens.css"),
            theme == "dark" ? @"html\[data-theme=""dark""\]\s*\{" : @":root,\s*html\[data-theme=""light""\]\s*\{");
        var mobile = Block(Read("src", "Monburan.Mobile", "wwwroot", "app.css"),
            theme == "dark" ? @":root,\s*html\[data-theme=""dark""\]\s*\{" : @"html\[data-theme=""light""\]\s*\{");

        var divergent = mobile
            .Where(token => web.TryGetValue(token.Key, out var value) && value != token.Value)
            .Select(token => $"{token.Key}: app {token.Value} × web {web[token.Key]}")
            .ToList();

        divergent.Should().BeEmpty($"o tema {theme} do app usa os mesmos valores da web");
        mobile.Keys.Should().Contain(["--bg", "--tx", "--tx-2", "--tx-muted", "--gold-d", "--er", "--focus-ring", "--border-input"]);
    }

    private static Dictionary<string, string> Block(string css, string selector)
    {
        var start = Regex.Match(css, selector);
        start.Success.Should().BeTrue($"bloco {selector} precisa existir");
        var body = css[(start.Index + start.Length)..css.IndexOf('}', start.Index)];
        return Declaration().Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
            dir = dir.Parent;
        return File.ReadAllText(Path.Combine([dir?.FullName ?? throw new DirectoryNotFoundException("Monburan.sln não encontrado"), .. parts]));
    }

    [GeneratedRegex(@"(--[\w-]+)\s*:\s*([^;]+);")]
    private static partial Regex Declaration();
}
