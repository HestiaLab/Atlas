namespace Atlas.Core.Operations;

/// <summary>
///     选择并激活现有工作区项。
/// </summary>
public sealed class SelectItemOperation : LayoutOperation
{
    /// <summary>
    ///     初始化选择操作。
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
    ///     获取要选择的项。
    /// </summary>
    public DockItemId ItemId { get; }
}
