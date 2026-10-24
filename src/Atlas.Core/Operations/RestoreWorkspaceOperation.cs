using Atlas.Core.Persistence;

namespace Atlas.Core.Operations;

/// <summary>
///     用已验证的持久化文档原子替换已提交状态。
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
    ///     获取已验证的恢复文档。
    /// </summary>
    public LayoutRestoreDocument Document { get; }
}
