namespace Atlas.Core.Placement;

/// <summary>
/// Stores the ordered committed auto-hide placement for one workspace edge.
/// </summary>
public sealed class AutoHideStripState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoHideStripState"/> class.
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
    /// Gets the workspace edge.
    /// </summary>
    public WorkspaceEdge Edge { get; }

    /// <summary>
    /// Gets the ordered auto-hide entries.
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
