namespace Atlas.Core.Operations;

/// <summary>
///     把工作区恢复至创建时的初始布局定义；提交会清空撤销与重做历史。
/// </summary>
public sealed class ResetWorkspaceOperation : LayoutOperation
{
    /// <summary>
    ///     初始化重置操作。
    /// </summary>
    /// <param name="baseRevision">可选的乐观并发修订号。</param>
    public ResetWorkspaceOperation(long? baseRevision = null)
        : base(LayoutOperationKind.ResetWorkspace, baseRevision)
    {
    }
}
