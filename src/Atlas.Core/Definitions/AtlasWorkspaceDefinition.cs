using System.Collections.ObjectModel;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Definitions;

/// <summary>
///     定义工作区的不可变初始已提交状态。
/// </summary>
public sealed class AtlasWorkspaceDefinition
{
    /// <summary>
    ///     构造不可变定义；工作区标识必须非默认，各集合立即复制为只读列表。
    /// </summary>
    public AtlasWorkspaceDefinition(
        WorkspaceId workspaceId,
        LayoutNodeId? rootNodeId,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items,
        IEnumerable<ToolBarState>? toolBars = null,
        IEnumerable<ToolBarOrderState>? toolBarOrders = null,
        IEnumerable<AutoHideStripState>? autoHideStrips = null)
    {
        if (workspaceId == default)
        {
            throw new ArgumentException(
                "A workspace identifier cannot be the default value.",
                nameof(workspaceId));
        }

        WorkspaceId = workspaceId;
        RootNodeId = rootNodeId;
        Nodes = Copy(nodes, nameof(nodes));
        Items = Copy(items, nameof(items));
        ToolBars = Copy(toolBars ?? Array.Empty<ToolBarState>(), nameof(toolBars));
        ToolBarOrders = Copy(
            toolBarOrders ?? Array.Empty<ToolBarOrderState>(),
            nameof(toolBarOrders));
        AutoHideStrips = Copy(
            autoHideStrips ?? Array.Empty<AutoHideStripState>(),
            nameof(autoHideStrips));
    }

    /// <summary>
    ///     获取稳定的工作区标识。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取布局根标识；空定义时为 null。
    /// </summary>
    public LayoutNodeId? RootNodeId { get; }

    /// <summary>
    ///     获取声明的布局节点。
    /// </summary>
    public IReadOnlyList<LayoutNode> Nodes { get; }

    /// <summary>
    ///     获取声明的停靠项。
    /// </summary>
    public IReadOnlyList<DockItem> Items { get; }

    /// <summary>
    ///     获取声明的工具栏状态。
    /// </summary>
    public IReadOnlyList<ToolBarState> ToolBars { get; }

    /// <summary>
    ///     获取声明的工具栏顺序状态。
    /// </summary>
    public IReadOnlyList<ToolBarOrderState> ToolBarOrders { get; }

    /// <summary>
    ///     获取声明的自动隐藏条。
    /// </summary>
    public IReadOnlyList<AutoHideStripState> AutoHideStrips { get; }

    private static IReadOnlyList<T> Copy<T>(
        IEnumerable<T> source,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        return new ReadOnlyCollection<T>(source.ToArray());
    }
}
