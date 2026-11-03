namespace Atlas.Core.Operations;

/// <summary>
///     取消浮动，并按返回书签把工具条目精确还原到浮出前的 placement。
/// </summary>
public sealed class DockFloatingToolOperation : LayoutOperation
{
    /// <summary>
    ///     初始化取消浮动操作。
    /// </summary>
    /// <param name="itemId">浮动窗口所属的工具条目标识。</param>
    /// <param name="baseRevision">可选的乐观并发基线。</param>
    public DockFloatingToolOperation(
        DockItemId itemId,
        long? baseRevision = null)
        : base(LayoutOperationKind.DockFloatingTool, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        ItemId = itemId;
    }

    /// <summary>
    ///     获取浮动窗口所属的工具条目标识。
    /// </summary>
    public DockItemId ItemId { get; }
}
