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

    [Fact]
    public void Srs_formal_blocks_and_appendix_define_the_same_150_requirements()
    {
        var srs = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "SRS.md"));
        var appendixStart = srs.IndexOf("## 附录A：需求统计", StringComparison.Ordinal);
        var appendixEnd = srs.IndexOf("## 附录 B：延期需求", StringComparison.Ordinal);

        Assert.True(appendixStart >= 0);
        Assert.True(appendixEnd > appendixStart);

        var formalIds = FormalRequirement.Matches(srs)
            .Select(match => match.Value.Split(' ')[0])
            .ToArray();
        var indexedIds = IndexedRequirement.Matches(srs[appendixStart..appendixEnd])
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(150, formalIds.Length);
        Assert.Equal(142, formalIds.Count(id => id.StartsWith("REQ-F-", StringComparison.Ordinal)));
        Assert.Equal(8, formalIds.Count(id => id.StartsWith("REQ-NF-", StringComparison.Ordinal)));
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
        var srs = File.ReadAllText(Path.Combine(root, "docs", "SRS.md"));
        var audit = File.ReadAllText(
            Path.Combine(root, "docs", "evidence", "p8-requirement-audit.md"));
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

        Assert.Equal(150, auditedRows.Length);
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
