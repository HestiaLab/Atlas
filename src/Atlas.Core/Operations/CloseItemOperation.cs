namespace Atlas.Core.Operations;

/// <summary>
///     在能力与守卫检查后关闭现有工作区项。
/// </summary>
public sealed class CloseItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 <see cref="CloseItemOperation"/> 类的新实例。
    /// </summary>
    public CloseItemOperation(DockItemId itemId, long? baseRevision = null)
        : base(LayoutOperationKind.CloseItem, baseRevision)
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
    ///     获取要关闭的项。
    /// </summary>
    public DockItemId ItemId { get; }
}
