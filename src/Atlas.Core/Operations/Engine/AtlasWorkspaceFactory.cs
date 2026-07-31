using Atlas.Core.Definitions;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class AtlasWorkspaceFactory : IAtlasWorkspaceFactory
{
    public WorkspaceResult<IAtlasWorkspace> Create(
        AtlasWorkspaceDefinition definition,
        AtlasWorkspaceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var resolvedOptions = options ?? new AtlasWorkspaceOptions();
        try
        {
            var snapshot = new WorkspaceSnapshot(
                definition.WorkspaceId,
                revision: 0,
                definition.RootNodeId,
                definition.Nodes.Select(
                    node => new KeyValuePair<LayoutNodeId, Layout.LayoutNode>(
                        node.Id,
                        node)),
                definition.Items.Select(
                    item => new KeyValuePair<DockItemId, Items.DockItem>(
                        item.Id,
                        item)),
                definition.ToolBars.Select(
                    toolbar => new KeyValuePair<Placement.LogicalSide, Placement.ToolBarState>(
                        toolbar.Side,
                        toolbar)),
                definition.ToolBarOrders.Select(
                    order => new KeyValuePair<Placement.LogicalRegion, Placement.ToolBarOrderState>(
                        order.Region,
                        order)),
                definition.AutoHideStrips.Select(
                    strip => new KeyValuePair<Placement.WorkspaceEdge, Placement.AutoHideStripState>(
                        strip.Edge,
                        strip)));
            IAtlasWorkspace workspace = new AtlasWorkspace(
                snapshot,
                new DefaultWorkspaceOperationPlanner(resolvedOptions),
                resolvedOptions.History.MaxEntries);
            return WorkspaceResult<IAtlasWorkspace>.Success(workspace);
        }
        catch (ArgumentException)
        {
            return InvalidDefinition();
        }
    }

    private static WorkspaceResult<IAtlasWorkspace> InvalidDefinition()
    {
        return WorkspaceResult<IAtlasWorkspace>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                LayoutOperationKind.ResetWorkspace,
                "ATLAS-DEFINITION-INVALID",
                "Atlas.Definition.Invalid"));
    }
}
