using System.Collections.ObjectModel;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core;

/// <summary>
/// Represents the immutable committed state of one Atlas workspace.
/// </summary>
public sealed class WorkspaceSnapshot
{
    internal WorkspaceSnapshot(
        WorkspaceId id,
        long revision,
        LayoutNodeId? rootNodeId,
        IEnumerable<KeyValuePair<LayoutNodeId, LayoutNode>> nodes,
        IEnumerable<KeyValuePair<DockItemId, DockItem>> items,
        IEnumerable<KeyValuePair<LogicalSide, ToolBarState>> toolBars,
        IEnumerable<KeyValuePair<LogicalRegion, ToolBarOrderState>> toolBarOrders,
        IEnumerable<KeyValuePair<WorkspaceEdge, AutoHideStripState>> autoHideStrips)
    {
        if (id == default)
        {
            throw new ArgumentException(
                "A workspace identifier cannot be the default value.",
                nameof(id));
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                revision,
                "A workspace revision cannot be negative.");
        }

        Id = id;
        Revision = revision;
        RootNodeId = rootNodeId;
        Nodes = Copy(nodes, nameof(nodes));
        Items = Copy(items, nameof(items));
        ToolBars = Copy(toolBars, nameof(toolBars));
        ToolBarOrders = Copy(toolBarOrders, nameof(toolBarOrders));
        AutoHideStrips = Copy(autoHideStrips, nameof(autoHideStrips));
    }

    /// <summary>
    /// Gets the workspace identity.
    /// </summary>
    public WorkspaceId Id { get; }

    /// <summary>
    /// Gets the monotonically increasing concurrency revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the layout tree root, or null for the unique empty-layout representation.
    /// </summary>
    public LayoutNodeId? RootNodeId { get; }

    /// <summary>
    /// Gets layout nodes indexed by identity.
    /// </summary>
    public IReadOnlyDictionary<LayoutNodeId, LayoutNode> Nodes { get; }

    /// <summary>
    /// Gets open dock items indexed by identity.
    /// </summary>
    public IReadOnlyDictionary<DockItemId, DockItem> Items { get; }

    /// <summary>
    /// Gets committed toolbar state by logical side.
    /// </summary>
    public IReadOnlyDictionary<LogicalSide, ToolBarState> ToolBars { get; }

    /// <summary>
    /// Gets toolbar-only ordering by logical region.
    /// </summary>
    public IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> ToolBarOrders { get; }

    /// <summary>
    /// Gets auto-hide placement by workspace edge.
    /// </summary>
    public IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> AutoHideStrips { get; }

    internal static WorkspaceSnapshot Empty(WorkspaceId id)
    {
        return new WorkspaceSnapshot(
            id,
            revision: 0,
            rootNodeId: null,
            Array.Empty<KeyValuePair<LayoutNodeId, LayoutNode>>(),
            Array.Empty<KeyValuePair<DockItemId, DockItem>>(),
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            Array.Empty<KeyValuePair<LogicalRegion, ToolBarOrderState>>(),
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
    }

    internal WorkspaceSnapshot WithRevision(long revision)
    {
        return new WorkspaceSnapshot(
            Id,
            revision,
            RootNodeId,
            Nodes,
            Items,
            ToolBars,
            ToolBarOrders,
            AutoHideStrips);
    }

    private static IReadOnlyDictionary<TKey, TValue> Copy<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        string parameterName)
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var copy = new Dictionary<TKey, TValue>();
        foreach (var pair in source)
        {
            copy.Add(pair.Key, pair.Value);
        }

        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }
}
