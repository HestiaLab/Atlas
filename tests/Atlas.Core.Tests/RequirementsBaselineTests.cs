using System.Text.RegularExpressions;
using Xunit;

namespace Atlas.Core.Tests;

public sealed class RequirementsBaselineTests
{
    private static readonly Regex FormalRequirement = new(
        @"^REQ-(F|NF)-\d{3} .*\[Actor:",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex IndexedRequirement = new(
        @"^\| (REQ-(?:F|NF)-\d{3}) \|",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private const string RequirementsIndexStart = "<!-- atlas:requirements-index:start -->";
    private const string RequirementsIndexEnd = "<!-- atlas:requirements-index:end -->";

    [Fact]
    public void Srs_formal_blocks_and_appendix_define_the_same_151_requirements()
    {
        var root = RepositoryRoot();
        var srs = File.ReadAllText(Path.Combine(root, "docs", "zh", "SRS.md"));
        var indexStart = srs.IndexOf(RequirementsIndexStart, StringComparison.Ordinal);
        var indexEnd = srs.IndexOf(RequirementsIndexEnd, StringComparison.Ordinal);

        Assert.True(indexStart >= 0);
        Assert.True(indexEnd > indexStart);

        var formalIds = FormalRequirement.Matches(srs)
            .Select(match => match.Value.Split(' ')[0])
            .ToArray();
        var indexedIds = IndexedRequirement.Matches(srs[indexStart..indexEnd])
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(151, formalIds.Length);
        Assert.Equal(142, formalIds.Count(id => id.StartsWith("REQ-F-", StringComparison.Ordinal)));
        Assert.Equal(9, formalIds.Count(id => id.StartsWith("REQ-NF-", StringComparison.Ordinal)));
        Assert.Equal(formalIds.Length, formalIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(indexedIds.Length, indexedIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            formalIds.OrderBy(id => id, StringComparer.Ordinal),
            indexedIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void P8_audit_has_one_evidence_status_for_every_formal_requirement()
    {
        var root = RepositoryRoot();
        var srs = File.ReadAllText(Path.Combine(root, "docs", "zh", "SRS.md"));
        var audit = File.ReadAllText(
            Path.Combine(root, "docs", "zh", "evidence", "p8-requirement-audit.md"));
        var formalIds = FormalRequirement.Matches(srs)
            .Select(match => match.Value.Split(' ')[0])
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var auditedRows = Regex.Matches(
                audit,
                @"^\| (REQ-(?:F|NF)-\d{3}) \| (?:Not Started|In Progress|Implemented|Verified|Blocked|Deferred) \|",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(151, auditedRows.Length);
        Assert.Equal(auditedRows.Length, auditedRows.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(formalIds, auditedRows.OrderBy(id => id, StringComparer.Ordinal));
        Assert.DoesNotContain("| Designed |", audit, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Atlas.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the Atlas repository root.");
    }
}
