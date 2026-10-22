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

    [Theory]
    [InlineData("base.css")]
    [InlineData("components.css")]
    [InlineData("shell.css")]
    [InlineData("utilities.css")]
    public void DesignSystemSheets_UseOnlyTokenColors(string sheet)
    {
        var css = ReadWithoutComments(Path.Combine(WebRoot, "wwwroot", sheet));

        HardcodedColor().Matches(css).Select(m => m.Value).Should().BeEmpty($"{sheet} usa cor por token");
    }

    [Fact]
    public void PagesSheet_UsesFixedColorsOnlyOnThePrintedCvSheet()
    {
        // Exceção documentada: a folha de CV (.cv-*) é papel neutro, igual nos dois temas e na impressão
        var css = ReadWithoutComments(Path.Combine(WebRoot, "wwwroot", "pages.css"));
        var offenders = Rule().Matches(css)
            .Where(rule => !rule.Groups["selector"].Value.Contains(".cv-") && !rule.Groups["selector"].Value.Contains("cv-print-area"))
            .SelectMany(rule => HardcodedColor().Matches(rule.Groups["body"].Value).Select(m => $"{rule.Groups["selector"].Value.Trim()}: {m.Value}"))
            .ToList();

        offenders.Should().BeEmpty("fora da folha de CV, cor só por token");
    }

    [Fact]
    public void Important_IsOnlyUsedToHonorReducedMotion()
    {
        // Sem camada legacy, a ordem das camadas resolve a precedência; !important só no bloco de movimento reduzido
        var offenders = Stylesheets()
            .Select(f => (File: Path.GetFileName(f), Css: ReadWithoutComments(f)))
            .Select(s => (s.File, Css: ReducedMotionBlock().Replace(s.Css, string.Empty)))
            .Where(s => s.Css.Contains("!important"))
            .Select(s => s.File)
            .ToList();

        offenders.Should().BeEmpty("!important fora de prefers-reduced-motion quebra a ordem das camadas");
    }

    [Fact]
    public void Stylesheets_UseOnlyTheSystemBreakpoints()
    {
        // 576, 768, 992 e 1200px (DESIGN.md); max-width usa o valor menos 0,02px
        var allowed = new[] { "576px", "768px", "992px", "1200px", "575.98px", "767.98px", "991.98px", "1199.98px" };
        var offenders = Stylesheets()
            .SelectMany(f => MediaWidth().Matches(ReadWithoutComments(f)).Select(m => (File: Path.GetFileName(f), Value: m.Groups[1].Value)))
            .Where(m => !allowed.Contains(m.Value))
            .Select(m => $"{m.File}: {m.Value}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("breakpoints fora da escala do sistema criam layouts intermediários não planejados");
    }

    [Fact]
    public void LegacyLayerIsGone()
    {
        var tokens = ReadWithoutComments(Path.Combine(WebRoot, "wwwroot", "tokens.css"));

        tokens.Should().Contain("@layer tokens, base, components, utilities;");
        Stylesheets().Select(ReadWithoutComments).Should().NotContain(css => css.Contains("layer(legacy)") || css.Contains("@layer legacy"));
        File.Exists(Path.Combine(WebRoot, "wwwroot", "ui.css")).Should().BeFalse();
        File.Exists(Path.Combine(WebRoot, "wwwroot", "jobs.css")).Should().BeFalse();
    }

    [Fact]
    public void Markup_UsesTheSystemButtonsAndIcons()
    {
        // Ação primária é .btn-primary (verde é estado, não ação); ícone é o componente Icon, nunca glifo de texto
        var offenders = Directory.EnumerateFiles(Path.Combine(WebRoot, "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (File: Path.GetFileName(file), Line: index + 1, Text: line)))
            .Where(item => !item.Text.TrimStart().StartsWith("@*") && (item.Text.Contains("btn-success") || GlyphIcon().IsMatch(item.Text)))
            .Select(item => $"{item.File}:{item.Line}")
            .ToList();

        offenders.Should().BeEmpty("use btn-primary e o componente Icon");
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

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{(?:[^{}]*\{[^{}]*\})*[^{}]*\}")]
    private static partial Regex ReducedMotionBlock();

    [GeneratedRegex(@"\((?:min|max)-width:\s*([\d.]+px)\)")]
    private static partial Regex MediaWidth();

    [GeneratedRegex(@"[✦✓⌕◈◌◎◷▣▤▦☰☆★↗▾▴⚠☾☀]")]
    private static partial Regex GlyphIcon();
}
