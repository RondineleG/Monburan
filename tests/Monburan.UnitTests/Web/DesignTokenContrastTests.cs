using System.Globalization;
using System.Text.RegularExpressions;

using FluentAssertions;

namespace Monburan.UnitTests.Web;

/// <summary>
/// Pares de cor que a interface usa, verificados nos dois temas a partir de wwwroot/tokens.css:
/// texto precisa de 4.5:1 (WCAG 1.4.3) e borda de controle ou anel de foco de 3:1 (WCAG 1.4.11).
/// Uma troca de token que derrube um par quebra este teste antes de chegar à tela.
/// </summary>
public partial class DesignTokenContrastTests
{
    private const double Text = 4.5;
    private const double NonText = 3.0;

    public static TheoryData<string, string, string, string?, double> Pairs()
    {
        var data = new TheoryData<string, string, string, string?, double>();
        foreach (var theme in new[] { "light", "dark" })
        {
            // texto principal e secundário sobre as superfícies
            data.Add(theme, "--tx", "--bg", null, Text);
            data.Add(theme, "--tx", "--bg-card", null, Text);
            data.Add(theme, "--tx-2", "--bg-card", null, Text);
            data.Add(theme, "--tx-2", "--bg-elev", null, Text);
            data.Add(theme, "--tx-muted", "--bg", null, Text);
            data.Add(theme, "--tx-muted", "--bg-card", null, Text);
            data.Add(theme, "--tx-muted", "--bg-elev", null, Text);
            data.Add(theme, "--tx-muted", "--bg-2", null, Text);
            data.Add(theme, "--tx-muted", "--bg-input", null, Text);
            // links, ação primária e destaques dourados
            data.Add(theme, "--gold-d", "--bg", null, Text);
            data.Add(theme, "--gold-d", "--bg-card", null, Text);
            data.Add(theme, "--gold-d", "--gold-soft", "--bg-card", Text);
            data.Add(theme, "--gold-d", "--topbar-bg", "--bg", Text);
            data.Add(theme, "--action-fg", "--action-bg", null, Text);
            data.Add(theme, "--gold-ink", "--gold", null, Text);
            // estados semânticos sobre o próprio fundo
            data.Add(theme, "--er", "--er-bg", "--bg-card", Text);
            data.Add(theme, "--er", "--bg-card", null, Text);
            data.Add(theme, "--er-ink", "--er", null, Text);
            data.Add(theme, "--ok", "--ok-bg", "--bg-card", Text);
            data.Add(theme, "--info-tx", "--info-bg", "--bg-card", Text);
            data.Add(theme, "--warn", "--gold-soft", "--bg-card", Text);
            // bordas de campo e anel de foco
            data.Add(theme, "--border-input", "--bg-input", null, NonText);
            data.Add(theme, "--border-input", "--bg-card", null, NonText);
            data.Add(theme, "--focus-ring", "--bg", null, NonText);
            data.Add(theme, "--focus-ring", "--bg-card", null, NonText);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void TokenPairMeetsWcagContrast(string theme, string foreground, string background, string? backdrop, double minimum)
    {
        var tokens = ThemeTokens(theme);
        var back = Resolve(tokens, background);
        if (back.A < 1)
            back = back.Over(Resolve(tokens, backdrop ?? "--bg"));
        var front = Resolve(tokens, foreground).Over(back);

        var ratio = Contrast(front, back);

        ratio.Should().BeGreaterThanOrEqualTo(minimum, $"{foreground} sobre {background} no tema {theme} precisa de {minimum}:1");
    }

    [Fact]
    public void EveryThemeDefinesTheSameColorTokens()
    {
        var light = ThemeTokens("light").Keys.ToHashSet();
        var dark = ThemeTokens("dark").Keys.ToHashSet();

        light.Except(dark).Should().BeEmpty("todo token claro precisa do par escuro");
        dark.Except(light).Should().BeEmpty("todo token escuro precisa do par claro");
    }

    private readonly record struct Rgba(double R, double G, double B, double A)
    {
        public Rgba Over(Rgba under) => new(R * A + under.R * (1 - A), G * A + under.G * (1 - A), B * A + under.B * (1 - A), 1);
    }

    private static Dictionary<string, string> ThemeTokens(string theme)
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Monburan.Web", "wwwroot", "tokens.css"));
        var selector = theme == "dark" ? @"html\[data-theme=""dark""\]\s*\{" : @":root,\s*html\[data-theme=""light""\]\s*\{";
        var start = Regex.Match(css, selector);
        start.Success.Should().BeTrue($"tokens.css precisa do bloco do tema {theme}");
        var body = css[(start.Index + start.Length)..css.IndexOf('}', start.Index)];
        return TokenDeclaration().Matches(body).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
    }

    private static Rgba Resolve(Dictionary<string, string> tokens, string name)
    {
        tokens.Should().ContainKey(name);
        var value = tokens[name];
        var reference = Regex.Match(value, @"^var\((--[\w-]+)\)$");
        return reference.Success ? Resolve(tokens, reference.Groups[1].Value) : Parse(value);
    }

    private static Rgba Parse(string value)
    {
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            return new(Convert.ToInt32(hex[..2], 16) / 255d, Convert.ToInt32(hex[2..4], 16) / 255d, Convert.ToInt32(hex[4..6], 16) / 255d, 1);
        }

        var numbers = Regex.Matches(value, @"-?\d+(\.\d+)?%?").Select(m => m.Value).ToArray();
        double Number(string n) => double.Parse(n.TrimEnd('%'), CultureInfo.InvariantCulture);
        double Alpha(int index) => numbers.Length > index ? (numbers[index].EndsWith('%') ? Number(numbers[index]) / 100 : Number(numbers[index])) : 1;

        if (value.StartsWith("rgb"))
            return new(Number(numbers[0]) / 255, Number(numbers[1]) / 255, Number(numbers[2]) / 255, Alpha(3));

        if (value.StartsWith("oklch"))
            return FromOklch(Number(numbers[0]), Number(numbers[1]), Number(numbers[2]), Alpha(3));

        throw new InvalidOperationException($"Formato de cor não suportado no teste: {value}");
    }

    // OKLCH -> OKLab -> sRGB linear -> sRGB (Björn Ottosson), com recorte na gama como o navegador faz.
    private static Rgba FromOklch(double l, double c, double hue, double alpha)
    {
        var h = hue * Math.PI / 180;
        var a = c * Math.Cos(h);
        var b = c * Math.Sin(h);

        var l_ = Math.Pow(l + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m_ = Math.Pow(l - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s_ = Math.Pow(l - 0.0894841775 * a - 1.2914855480 * b, 3);

        var r = 4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_;
        var g = -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_;
        var bl = -0.0041960863 * l_ - 0.7034186147 * m_ + 1.7076147010 * s_;

        static double Encode(double v)
        {
            v = Math.Clamp(v, 0, 1);
            return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
        }

        return new(Encode(r), Encode(g), Encode(bl), alpha);
    }

    private static double Contrast(Rgba x, Rgba y)
    {
        static double Luminance(Rgba c)
        {
            static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        var (lx, ly) = (Luminance(x), Luminance(y));
        return (Math.Max(lx, ly) + 0.05) / (Math.Min(lx, ly) + 0.05);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Monburan.sln não encontrado");
    }

    [GeneratedRegex(@"(--[\w-]+)\s*:\s*([^;]+);")]
    private static partial Regex TokenDeclaration();
}
