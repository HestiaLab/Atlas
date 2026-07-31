using System.Collections.ObjectModel;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Definitions;

/// <summary>Defines the immutable initial committed state for a workspace.</summary>
public sealed class AtlasWorkspaceDefinition
{
    /// <summary>Initializes a workspace definition.</summary>
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

    /// <summary>Gets the stable workspace identity.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the layout root identity, or null for an empty definition.</summary>
    public LayoutNodeId? RootNodeId { get; }

    /// <summary>Gets the declared layout nodes.</summary>
    public IReadOnlyList<LayoutNode> Nodes { get; }

    /// <summary>Gets the declared dock items.</summary>
    public IReadOnlyList<DockItem> Items { get; }

    /// <summary>Gets the declared toolbar states.</summary>
    public IReadOnlyList<ToolBarState> ToolBars { get; }

    /// <summary>Gets the declared toolbar order states.</summary>
    public IReadOnlyList<ToolBarOrderState> ToolBarOrders { get; }

    /// <summary>Gets the declared auto-hide strips.</summary>
    public IReadOnlyList<AutoHideStripState> AutoHideStrips { get; }

    private static IReadOnlyList<T> Copy<T>(
        IEnumerable<T> source,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        return new ReadOnlyCollection<T>(source.ToArray());
    }
}
