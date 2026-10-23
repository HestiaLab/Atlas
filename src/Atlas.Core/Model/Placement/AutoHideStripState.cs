namespace Atlas.Core.Placement;

/// <summary>
///     An auto-hide strip on one workspace edge: an ordered entry list whose item identifiers must be unique.
/// </summary>
public sealed class AutoHideStripState
{
    /// <summary>
    ///     Constructs an auto-hide strip; the edge must be a known value and entries must be non-default with unique identifiers.
    /// </summary>
    public AutoHideStripState(WorkspaceEdge edge, IEnumerable<AutoHideEntry> entries)
    {
        ValidateEdge(edge);
        ArgumentNullException.ThrowIfNull(entries);

        var materializedEntries = entries.ToArray();
        if (materializedEntries.Any(entry => entry.IsDefault))
        {
            throw new ArgumentException(
                "An auto-hide strip cannot contain a default entry.",
                nameof(entries));
        }

        if (materializedEntries.Select(entry => entry.ItemId).Distinct().Count()
            != materializedEntries.Length)
        {
            throw new ArgumentException(
                "An auto-hide strip cannot contain duplicate item identifiers.",
                nameof(entries));
        }

        Edge = edge;
        Entries = Array.AsReadOnly(materializedEntries);
    }

    /// <summary>
    ///     The workspace edge this strip is attached to.
    /// </summary>
    public WorkspaceEdge Edge { get; }

    /// <summary>
    ///     The auto-hide entries in display order (read-only).
    /// </summary>
    public IReadOnlyList<AutoHideEntry> Entries { get; }

    private static void ValidateEdge(WorkspaceEdge edge)
    {
        if (edge is not WorkspaceEdge.InlineStart
            and not WorkspaceEdge.InlineEnd
            and not WorkspaceEdge.BlockEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown workspace edge.");
        }
    }
}
