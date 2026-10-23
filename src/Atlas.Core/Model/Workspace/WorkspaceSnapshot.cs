using System.Collections.ObjectModel;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core;

/// <summary>
///     Immutable committed snapshot of a workspace: layout nodes, items, tool bars, and auto-hide placement
///     are all indexed by identifier; tool group visibility is normalized through GroupVisibilityInvariant at construction.
/// </summary>
public sealed class WorkspaceSnapshot
{
    private WorkspaceSnapshot(
        WorkspaceId id,
        long revision,
        LayoutNodeId? rootNodeId,
        IReadOnlyDictionary<LayoutNodeId, LayoutNode> nodes,
        IReadOnlyDictionary<DockItemId, DockItem> items,
        IReadOnlyDictionary<LogicalSide, ToolBarState> toolBars,
        IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> toolBarOrders,
        IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> autoHideStrips,
        bool trusted)
    {
        Id = id;
        Revision = revision;
        RootNodeId = rootNodeId;
        Nodes = nodes;
        Items = items;
        ToolBars = toolBars;
        ToolBarOrders = toolBarOrders;
        AutoHideStrips = autoHideStrips;
    }

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
        // 规范化工具组可见性，使每个已提交的快照都满足 REQ-F-154：
        // 空的 Persistent 工具组折叠；非空组保持调用方给定的可见性。
        // 参见 GroupVisibilityInvariant。
        Nodes = GroupVisibilityInvariant.Normalize(Copy(nodes, nameof(nodes)));
        Items = Copy(items, nameof(items));
        ToolBars = Copy(toolBars, nameof(toolBars));
        ToolBarOrders = Copy(toolBarOrders, nameof(toolBarOrders));
        AutoHideStrips = Copy(autoHideStrips, nameof(autoHideStrips));
    }

    /// <summary>
    ///     The workspace identifier.
    /// </summary>
    public WorkspaceId Id { get; }

    /// <summary>
    ///     The monotonically increasing revision: incremented on every commit and also the baseline for
    ///     optimistic concurrency - operations carry a BaseRevision and are rejected with an expected failure when it does not match the current revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    ///     The identifier of the layout tree root node; <see langword="null"/> represents the single empty layout.
    /// </summary>
    public LayoutNodeId? RootNodeId { get; }

    /// <summary>
    ///     Layout tree nodes indexed by node identifier (read-only).
    /// </summary>
    public IReadOnlyDictionary<LayoutNodeId, LayoutNode> Nodes { get; }

    /// <summary>
    ///     Open dock items indexed by item identifier (read-only).
    /// </summary>
    public IReadOnlyDictionary<DockItemId, DockItem> Items { get; }

    /// <summary>
    ///     Committed tool bar states indexed by logical side (read-only).
    /// </summary>
    public IReadOnlyDictionary<LogicalSide, ToolBarState> ToolBars { get; }

    /// <summary>
    ///     Tool bar orders indexed by logical region (read-only).
    /// </summary>
    public IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> ToolBarOrders { get; }

    /// <summary>
    ///     Auto-hide placement indexed by workspace edge (read-only).
    /// </summary>
    public IReadOnlyDictionary<WorkspaceEdge, AutoHideStripState> AutoHideStrips { get; }

    /// <summary>
    ///     创建指定工作区的空快照：修订为 0 且无任何内容。
    /// </summary>
    /// <param name="id">工作区标识。</param>
    /// <returns>
    ///     修订为 0 且无任何内容的空快照。
    /// </returns>
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

    /// <summary>
    ///     用新的已提交状态替换内容，保留工作区标识与修订号。
    /// </summary>
    internal WorkspaceSnapshot WithState(
        LayoutNodeId? rootNodeId,
        IEnumerable<KeyValuePair<LayoutNodeId, LayoutNode>> nodes,
        IEnumerable<KeyValuePair<DockItemId, DockItem>> items,
        IEnumerable<KeyValuePair<LogicalSide, ToolBarState>> toolBars,
        IEnumerable<KeyValuePair<LogicalRegion, ToolBarOrderState>> toolBarOrders,
        IEnumerable<KeyValuePair<WorkspaceEdge, AutoHideStripState>> autoHideStrips)
    {
        return new WorkspaceSnapshot(
            Id,
            Revision,
            rootNodeId,
            nodes,
            items,
            toolBars,
            toolBarOrders,
            autoHideStrips);
    }

    /// <summary>
    ///     复制快照并仅替换修订号；五个索引直接共享引用（快照不可变，共享安全）。
    /// </summary>
    /// <param name="revision">新修订号。</param>
    /// <returns>
    ///     与当前快照等价但修订号已替换的副本。
    /// </returns>
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
            AutoHideStrips,
            trusted: true);
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
