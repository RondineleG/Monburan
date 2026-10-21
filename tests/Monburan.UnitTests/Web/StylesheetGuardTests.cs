using System.Text.RegularExpressions;

using FluentAssertions;

namespace Monburan.UnitTests.Web;

/// <summary>
/// Guardas do design system sobre as folhas de estilo do site: evitam que a interface volte a depender
/// de variáveis inexistentes (que caem no valor de reserva, sem tema) ou de cor fixa fora dos tokens.
/// </summary>
public partial class StylesheetGuardTests
{
    private static readonly string WebRoot = Path.Combine(RepoRoot(), "src", "Monburan.Web");

    private static IEnumerable<string> Stylesheets() =>
        Directory.EnumerateFiles(Path.Combine(WebRoot, "wwwroot"), "*.css", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(Path.Combine(WebRoot, "Components"), "*.razor.css", SearchOption.AllDirectories));

    private static string ReadWithoutComments(string file) => Comment().Replace(File.ReadAllText(file), string.Empty);

    [Fact]
    public void EveryCustomPropertyUsedIsDefined()
    {
        var sources = Stylesheets().ToDictionary(f => Path.GetFileName(f), ReadWithoutComments);
        var defined = sources.Values.SelectMany(css => Declaration().Matches(css).Select(m => m.Groups[1].Value)).ToHashSet();

        var missing = sources
            .SelectMany(s => Usage().Matches(s.Value).Select(m => (File: s.Key, Name: m.Groups[1].Value)))
            .Where(u => !defined.Contains(u.Name))
            .Select(u => $"{u.File}: {u.Name}")
            .Distinct()
            .ToList();

        missing.Should().BeEmpty("toda variável usada precisa existir em tokens.css (ou ser declarada localmente)");
    }

    [Fact]
    public void TokensOnlyLiveInTokensCss()
    {
        // Os temas são definidos uma vez: outra folha redefinindo --bg/--tx criaria um terceiro tema escondido
        var themeTokens = new[] { "--bg", "--bg-card", "--tx", "--tx-2", "--tx-muted", "--gold", "--gold-d", "--action-bg", "--er", "--ok", "--border", "--focus-ring" };
        var offenders = Stylesheets()
            .Where(f => Path.GetFileName(f) != "tokens.css")
            .SelectMany(f => Declaration().Matches(ReadWithoutComments(f)).Select(m => (File: Path.GetFileName(f), Name: m.Groups[1].Value)))
            .Where(d => themeTokens.Contains(d.Name))
            .Select(d => $"{d.File}: {d.Name}")
            .ToList();

        offenders.Should().BeEmpty("cores de tema só se declaram em tokens.css");
    }

    [Fact]
    public void ComponentsLayer_UsesOnlyTokens_AndNoImportant()
    {
        var css = ReadWithoutComments(Path.Combine(WebRoot, "wwwroot", "components.css"));

        HardcodedColor().Matches(css).Select(m => m.Value).Should().BeEmpty("componentes usam cor por token");
        css.Should().NotContain("!important", "a camada components vence legacy pela ordem, sem !important");
    }

    [Fact]
    public void Markup_DoesNotUseIconOrScriptClassesFromBootstrap()
    {
        // O Bootstrap (CSS, ícones e JS) não é carregado: bi-*, glyphicon e data-bs-* não fazem nada na tela
        var offenders = Directory.EnumerateFiles(Path.Combine(WebRoot, "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (File: Path.GetFileName(file), Line: index + 1, Text: line)))
            .Where(item => BootstrapOnlyMarkup().IsMatch(item.Text))
            .Select(item => $"{item.File}:{item.Line}")
            .ToList();

        offenders.Should().BeEmpty("use o componente Icon e os comportamentos do próprio site");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Monburan.sln não encontrado");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<![\w-])(--[\w-]+)\s*:")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"var\(\s*(--[\w-]+)")]
    private static partial Regex Usage();

    [GeneratedRegex(@"class=""[^""]*\b(bi|glyphicon)\b|\bbi-[a-z]|data-bs-")]
    private static partial Regex BootstrapOnlyMarkup();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\boklch\(|\bhsla?\(")]
    private static partial Regex HardcodedColor();
}
