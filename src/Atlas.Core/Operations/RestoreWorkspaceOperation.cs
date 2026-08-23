using Atlas.Core.Persistence;

namespace Atlas.Core.Operations;

/// <summary>
///     原子地用经过验证的持久化文档替换已提交状态。
/// </summary>
public sealed class RestoreWorkspaceOperation : LayoutOperation
{
    /// <summary>
    ///     初始化恢复请求。
    /// </summary>
    public RestoreWorkspaceOperation(
        LayoutRestoreDocument document,
        long? baseRevision = null)
        : base(LayoutOperationKind.RestoreWorkspace, baseRevision)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    ///     获取经过验证的恢复文档。
    /// </summary>
    public LayoutRestoreDocument Document { get; }
}
