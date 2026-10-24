namespace Atlas.Core.Operations;

/// <summary>
///     选中并激活现有工作区条目。
/// </summary>
public sealed class SelectItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化选中操作。
    /// </summary>
    public SelectItemOperation(DockItemId itemId, long? baseRevision = null)
        : base(LayoutOperationKind.SelectItem, baseRevision)
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
    ///     获取要选中的条目。
    /// </summary>
    public DockItemId ItemId { get; }
}
