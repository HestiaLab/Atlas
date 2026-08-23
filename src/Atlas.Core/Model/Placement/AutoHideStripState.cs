namespace Atlas.Core.Placement;

/// <summary>
///     一个工作区边缘的自动隐藏条：有序条目列表，条目标识不得重复。
/// </summary>
public sealed class AutoHideStripState
{
    /// <summary>
    ///     构造自动隐藏条；边缘必须为已知值，条目不得为默认值且标识不得重复。
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
    ///     该条挂靠的工作区边缘。
    /// </summary>
    public WorkspaceEdge Edge { get; }

    /// <summary>
    ///     按显示顺序排列的自动隐藏条目（只读）。
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
