using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>Provides convenience commands that delegate to workspace operations.</summary>
public sealed class AtlasWorkspaceCommands
{
    private readonly IAtlasWorkspace workspace;

    /// <summary>Initializes a command facade for one workspace.</summary>
    public AtlasWorkspaceCommands(IAtlasWorkspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>Opens an item in an explicit target group.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> OpenItemAsync(
        DockItem item,
        LayoutNodeId targetGroupId,
        ContentDeduplicationMode deduplicationMode,
        int? targetIndex = null,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new OpenItemOperation(
                item,
                targetGroupId,
                deduplicationMode,
                targetIndex,
                baseRevision),
            cancellationToken);
    }

    /// <summary>Closes an existing item.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseItemAsync(
        DockItemId itemId,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new CloseItemOperation(itemId, baseRevision),
            cancellationToken);
    }

    /// <summary>Activates an existing item.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateItemAsync(
        DockItemId itemId,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new SelectItemOperation(itemId, baseRevision),
            cancellationToken);
    }

    /// <summary>Changes visibility for a tool group.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> SetToolVisibilityAsync(
        LayoutNodeId groupId,
        bool visible,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                groupId,
                visible ? GroupVisibility.Expanded : GroupVisibility.Collapsed,
                baseRevision),
            cancellationToken);
    }

    /// <summary>Changes visibility for a logical toolbar.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> SetToolBarVisibilityAsync(
        LogicalSide side,
        ToolBarVisibility visibility,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new SetToolBarVisibilityOperation(side, visibility, baseRevision),
            cancellationToken);
    }

    /// <summary>Activates the first item matching a snapshot predicate.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateFirstAsync(
        Func<DockItem, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var snapshot = workspace.Current;
        var item = snapshot.Items.Values.FirstOrDefault(predicate);
        return item is null
            ? ValueTask.FromResult(
                WorkspaceResult<LayoutChangeSet>.NoChange(
                    LayoutChangeSet.Empty(
                        snapshot.Revision,
                        LayoutChangeKind.Operation)))
            : workspace.ExecuteAsync(
                new SelectItemOperation(item.Id, snapshot.Revision),
                cancellationToken);
    }
}
