namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工作区存储状态：当前已提交快照与撤销/重做补丁栈。
/// </summary>
internal sealed class WorkspaceStoreState
{
    internal WorkspaceStoreState(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<WorkspacePatch>? undo = null,
        IReadOnlyList<WorkspacePatch>? redo = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Undo = undo ?? Array.Empty<WorkspacePatch>();
        Redo = redo ?? Array.Empty<WorkspacePatch>();
    }

    /// <summary>
    ///     获取当前已提交的工作区快照。
    /// </summary>
    internal WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     获取可撤销的补丁栈，栈顶为最近一次提交。
    /// </summary>
    internal IReadOnlyList<WorkspacePatch> Undo { get; }

    /// <summary>
    ///     获取已撤销的补丁栈，撤销时累积于此，供重做弹回。
    /// </summary>
    internal IReadOnlyList<WorkspacePatch> Redo { get; }
}
