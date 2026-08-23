using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Persistence;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     为工作区操作提供类型化便捷命令门面；
///     每个命令构造不可变操作并经 ExecuteAsync 原子提交，行为与直接调用操作引擎一致。
/// </summary>
public sealed class AtlasWorkspaceCommands
{
    // 命令门面代理的工作区；所有命令最终都提交到它。
    private readonly IAtlasWorkspace workspace;

    /// <summary>
    ///     为单个工作区构造命令门面。
    /// </summary>
    public AtlasWorkspaceCommands(IAtlasWorkspace workspace)
    {
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>
    ///     在显式目标组中打开项；去重模式决定内容等价项的处理，
    ///     baseRevision 与当前修订不一致时返回 Stale。
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
    ///     按标识关闭项；baseRevision 与当前修订不一致时返回 Stale。
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
    ///     激活项并更新所在组的选中状态；选中被折叠的工具组会展开它，
    ///     baseRevision 与当前修订不一致时返回 Stale。
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
    ///     切换工具组的展开/折叠可见性。
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
    ///     更改逻辑工具栏的可见性。
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
    ///     原子地恢复已验证的持久化布局并返回其报告；
    ///     报告中携带变更集与重映射/占位/剔除的处理结果。
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
    ///     激活当前快照中首个匹配谓词的项；无匹配项时返回 NoChange。
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
