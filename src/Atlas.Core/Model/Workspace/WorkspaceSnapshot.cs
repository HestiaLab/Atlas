using System.Collections.ObjectModel;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;

namespace Atlas.Core;

/// <summary>
///     工作区的不可变已提交快照：布局节点、项、工具栏与自动隐藏放置
///     全部按标识索引；构造时经 GroupVisibilityInvariant 规范化工具组可见性。
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
        // 规范化工具组可见性，使每个已提交的快照都满足 REQ-F-154
        // （空的 Persistent 工具组折叠，非空的工具组展开）。
        // 参见 GroupVisibilityInvariant。
        Nodes = GroupVisibilityInvariant.Normalize(Copy(nodes, nameof(nodes)));
        Items = Copy(items, nameof(items));
        ToolBars = Copy(toolBars, nameof(toolBars));
        ToolBarOrders = Copy(toolBarOrders, nameof(toolBarOrders));
        AutoHideStrips = Copy(autoHideStrips, nameof(autoHideStrips));
    }

    /// <summary>
    ///     工作区标识。
    /// </summary>
    public WorkspaceId Id { get; }

    /// <summary>
    ///     单调递增的修订号：每次提交递增，同时作为乐观并发的基线——
    ///     操作携带 BaseRevision，与当前修订不符时以预期失败拒绝。
    /// </summary>
    public long Revision { get; }

    /// <summary>
    ///     布局树根节点标识；<see langword="null"/> 表示唯一的空布局。
    /// </summary>
    public LayoutNodeId? RootNodeId { get; }

    /// <summary>
    ///     按节点标识索引的布局树节点（只读）。
    /// </summary>
    public IReadOnlyDictionary<LayoutNodeId, LayoutNode> Nodes { get; }

    /// <summary>
    ///     按项标识索引的已打开停靠项（只读）。
    /// </summary>
    public IReadOnlyDictionary<DockItemId, DockItem> Items { get; }

    /// <summary>
    ///     按逻辑侧索引的已提交工具栏状态（只读）。
    /// </summary>
    public IReadOnlyDictionary<LogicalSide, ToolBarState> ToolBars { get; }

    /// <summary>
    ///     按逻辑区域索引的仅工具栏排序（只读）。
    /// </summary>
    public IReadOnlyDictionary<LogicalRegion, ToolBarOrderState> ToolBarOrders { get; }

    /// <summary>
    ///     按工作区边缘索引的自动隐藏放置（只读）。
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
    ///     复制快照并仅替换修订号；其余集合直接共享引用（快照不可变，共享安全）。
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
