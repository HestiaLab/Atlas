#nullable enable
using System.Text.RegularExpressions;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class CssTokenContractTests
{
    private static readonly Regex DefinitionPattern = new(
        @"(?<name>--atlas-[a-z0-9-]+)\s*:",
        RegexOptions.CultureInvariant);

    private static readonly Regex UsagePattern = new(
        @"var\(\s*(?<name>--atlas-[a-z0-9-]+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DocumentedPattern = new(
        @"`(?<name>--atlas-[a-z0-9-]+)`",
        RegexOptions.CultureInvariant);

    private static readonly Regex CssCodeBlockPattern = new(
        @"```css\s*(?<code>.*?)```",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex DeclarationPattern = new(
        @"^\s*(?<name>--?[a-z0-9-]+)\s*:",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [Fact]
    public void Every_documented_token_is_defined_and_used_by_the_default_stylesheet()
    {
        var css = ReadFixture("atlas.css");
        var theming = ReadFixture("theming.md");
        var defined = Matches(css, DefinitionPattern);
        var used = Matches(css, UsagePattern);
        var documented = Matches(theming, DocumentedPattern)
            .Where(name => name.Length > "--atlas-".Length)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(documented);
        foreach (var token in documented)
        {
            Assert.Contains(token, defined);
            Assert.Contains(token, used);
        }

        // 反向契约：样式表新增的主题变量必须同步登记到公开文档，
        // 否则消费方无法知道可以覆盖它（REQ-NF-009 AC1）。
        Assert.All(
            defined,
            token => Assert.Contains(token, documented));
    }

    [Fact]
    public void Default_stylesheet_does_not_require_important_or_internal_selector_overrides()
    {
        var css = ReadFixture("atlas.css");
        var theming = ReadFixture("theming.md");

        Assert.DoesNotContain("!important", css, StringComparison.OrdinalIgnoreCase);
        // 文档明确承诺不需要 !important；示例代码块本身只能声明 --atlas-* 变量。
        Assert.Contains("不需要 `!important`", theming, StringComparison.Ordinal);
        var codeBlocks = CssCodeBlockPattern.Matches(theming);
        Assert.NotEmpty(codeBlocks);
        foreach (Match block in codeBlocks)
        {
            var code = block.Groups["code"].Value;
            Assert.DoesNotContain("!important", code, StringComparison.OrdinalIgnoreCase);
            var declarations = DeclarationPattern.Matches(code);
            Assert.NotEmpty(declarations);
            Assert.All(
                declarations,
                declaration => Assert.StartsWith(
                    "--atlas-",
                    declaration.Groups["name"].Value,
                    StringComparison.Ordinal));
        }

        // 覆盖示例必须落在工作区容器/祖先上，而不是内部组件选择器。
        Assert.Contains(".atlas-v2-workspace", theming, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_stylesheet_only_uses_documented_tokens()
    {
        var theme = ReadFixture("ide.css");
        var documented = Matches(ReadFixture("theming.md"), DocumentedPattern)
            .Where(name => name.Length > "--atlas-".Length)
            .ToHashSet(StringComparer.Ordinal);

        var defined = Matches(theme, DefinitionPattern);
        var used = Matches(theme, UsagePattern);

        Assert.NotEmpty(defined);
        Assert.NotEmpty(used);
        Assert.All(defined, token => Assert.Contains(token, documented));
        Assert.All(used, token => Assert.Contains(token, documented));
    }

    [Fact]
    public void Theme_stylesheet_provides_light_and_dark_palettes_without_important()
    {
        var theme = ReadFixture("ide.css");

        Assert.Contains(
            "@media (prefers-color-scheme: dark)",
            theme,
            StringComparison.Ordinal);
        Assert.Contains("color-scheme: light dark", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("!important", theme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--atlas-group-border-width: 1px", theme, StringComparison.Ordinal);
        Assert.Contains("--atlas-tab-indicator-width: 2px", theme, StringComparison.Ordinal);
    }

    private static HashSet<string> Matches(string text, Regex pattern)
    {
        return pattern.Matches(text)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", fileName));
    }
}
