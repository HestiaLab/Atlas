#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ThemeContrastTests
{
    private static readonly Regex RootBlockPattern = new(
        @"\.atlas-v2-workspace\s*\{(?<body>[^}]*)\}",
        RegexOptions.CultureInvariant);

    private static readonly Regex DarkBlockPattern = new(
        @"@media\s*\(\s*prefers-color-scheme:\s*dark\s*\)\s*\{\s*\.atlas-v2-workspace\s*\{(?<body>[^}]*)\}",
        RegexOptions.CultureInvariant);

    private static readonly Regex ColorPattern = new(
        @"(?<name>--ide-[a-z0-9-]+)\s*:\s*(?<value>#[0-9a-fA-F]{6})\s*;",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Theme_palette_pairs_meet_wcag_aa_contrast()
    {
        var theme = ReadFixture("ide.css");
        var light = ReadPalette(RootBlockPattern.Match(theme));
        var dark = ReadPalette(DarkBlockPattern.Match(theme));

        Assert.NotEmpty(light);
        Assert.All(light.Keys, key => Assert.Contains(key, dark));

        var pairs = new (string Foreground, string Background, double Minimum, string Label)[]
        {
            ("text", "surface", 4.5, "primary text on surface"),
            ("text", "bg", 4.5, "primary text on workspace"),
            ("text", "chrome", 4.5, "primary text on chrome"),
            ("text", "selection", 4.5, "selected toolbar text"),
            ("text-secondary", "surface", 4.5, "secondary text on surface"),
            ("text-secondary", "chrome", 4.5, "secondary text on chrome"),
            ("text-secondary", "bg", 4.5, "secondary text on workspace"),
            ("accent", "surface", 3.0, "accent indicator on surface"),
            ("accent", "bg", 3.0, "accent indicator on workspace"),
            ("focus", "surface", 3.0, "focus ring on surface"),
            ("on-accent", "accent", 4.5, "text on accent"),
            ("danger-text", "danger-bg", 4.5, "danger text"),
            ("warning-text", "warning-bg", 4.5, "warning text"),
        };

        AssertPalettePasses("light", light, pairs);
        AssertPalettePasses("dark", dark, pairs);
    }

    private static void AssertPalettePasses(
        string scheme,
        IReadOnlyDictionary<string, string> palette,
        IEnumerable<(string Foreground, string Background, double Minimum, string Label)> pairs)
    {
        foreach (var (foreground, background, minimum, label) in pairs)
        {
            var ratio = Contrast(palette[foreground], palette[background]);
            Assert.True(
                ratio >= minimum,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}: {2:F2}:1 is below {3:F1}:1",
                    scheme,
                    label,
                    ratio,
                    minimum));
        }
    }

    private static Dictionary<string, string> ReadPalette(Match match)
    {
        Assert.True(match.Success, "Theme palette block was not found.");
        return ColorPattern.Matches(match.Groups["body"].Value)
            .ToDictionary(
                color => color.Groups["name"].Value["--ide-".Length..],
                color => color.Groups["value"].Value,
                StringComparer.Ordinal);
    }

    private static double Contrast(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var red = Channel(hex, 1);
        var green = Channel(hex, 3);
        var blue = Channel(hex, 5);
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double Channel(string hex, int offset)
    {
        var value = int.Parse(
            hex.AsSpan(offset, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture) / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", fileName));
    }
}
