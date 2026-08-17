using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     声明式定义物化器：将工作区定义转换为不可变的工作区快照。
/// </summary>
internal static class WorkspaceDefinitionMaterializer
{
    /// <summary>
    ///     将定义中的布局、项、工具栏与自动隐藏条装配为指定修订号的不可变快照。
    /// </summary>
    /// <param name="definition">声明式工作区定义。</param>
    /// <param name="revision">新快照的修订号。</param>
    /// <returns>
    ///     装配完成的不可变快照。
    /// </returns>
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
