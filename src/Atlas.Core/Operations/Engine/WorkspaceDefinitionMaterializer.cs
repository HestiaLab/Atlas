using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

internal static class WorkspaceDefinitionMaterializer
{
    internal static WorkspaceSnapshot Create(
        AtlasWorkspaceDefinition definition,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkspaceSnapshot(
            definition.WorkspaceId,
            revision,
            definition.RootNodeId,
            definition.Nodes.Select(
                node => new KeyValuePair<LayoutNodeId, LayoutNode>(
                    node.Id,
                    node)),
            definition.Items.Select(
                item => new KeyValuePair<DockItemId, DockItem>(
                    item.Id,
                    item)),
            definition.ToolBars.Select(
                toolbar => new KeyValuePair<LogicalSide, ToolBarState>(
                    toolbar.Side,
                    toolbar)),
            definition.ToolBarOrders.Select(
                order => new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    order.Region,
                    order)),
            definition.AutoHideStrips.Select(
                strip => new KeyValuePair<WorkspaceEdge, AutoHideStripState>(
                    strip.Edge,
                    strip)));
    }
}
