using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Persistence;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     Provides a typed convenience command facade over workspace operations;
///     each command builds an immutable operation and commits it atomically through ExecuteAsync,
///     with the same behavior as calling the operation engine directly.
/// </summary>
public sealed class AtlasWorkspaceCommands
{
    // 命令门面代理的工作区；所有命令最终都提交到它。
    private readonly IAtlasWorkspace workspace;

    /// <summary>
    ///     Initializes a command facade for a single workspace.
    /// </summary>
    public AtlasWorkspaceCommands(IAtlasWorkspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>
    ///     Opens an item in an explicit target group; the deduplication mode decides how
    ///     content-equivalent items are handled, and Stale is returned when baseRevision
    ///     differs from the current revision.
    /// </summary>
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

    /// <summary>
    ///     Closes an item by identifier; Stale is returned when baseRevision differs from
    ///     the current revision.
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseItemAsync(
        DockItemId itemId,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new CloseItemOperation(itemId, baseRevision),
            cancellationToken);
    }

    /// <summary>
    ///     Activates an item and updates the selection state of its group; selecting a
    ///     collapsed tool group expands it, and Stale is returned when baseRevision differs
    ///     from the current revision.
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateItemAsync(
        DockItemId itemId,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new SelectItemOperation(itemId, baseRevision),
            cancellationToken);
    }

    /// <summary>
    ///     Toggles the expanded/collapsed visibility of a tool group.
    /// </summary>
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

    /// <summary>
    ///     Changes the visibility of a logical tool bar.
    /// </summary>
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

    /// <summary>
    ///     Atomically restores a validated persistent layout and returns its report;
    ///     the report carries the change set and the outcome of remapping, placeholders,
    ///     and removals.
    /// </summary>
    public async ValueTask<WorkspaceResult<LayoutRestoreResult>> RestoreAsync(
        LayoutRestoreDocument document,
        long? baseRevision = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(document, baseRevision),
            cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return WorkspaceResult<LayoutRestoreResult>.Failure(result.Error!);
        }

        var restored = new LayoutRestoreResult(result.Value!, document);
        return result.IsNoChange
            ? WorkspaceResult<LayoutRestoreResult>.NoChange(restored)
            : WorkspaceResult<LayoutRestoreResult>.Success(restored);
    }

    /// <summary>
    ///     Activates the first item in the current snapshot that matches the predicate;
    ///     returns NoChange when no item matches.
    /// </summary>
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
