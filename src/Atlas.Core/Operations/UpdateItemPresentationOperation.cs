namespace Atlas.Core.Operations;

/// <summary>
///     更新条目呈现元数据（标题与图标键），与任何 UI 无关。
/// </summary>
public sealed class UpdateItemPresentationOperation : LayoutOperation
{
    /// <summary>
    ///     初始化条目呈现操作。
    /// </summary>
    public UpdateItemPresentationOperation(
        DockItemId itemId,
        string? title,
        string? iconKey,
        long? baseRevision = null)
        : base(LayoutOperationKind.UpdateItemPresentation, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (title is not null && string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "A specified title cannot be empty or whitespace.",
                nameof(title));
        }

        if (iconKey is not null && string.IsNullOrWhiteSpace(iconKey))
        {
            throw new ArgumentException(
                "A specified icon key cannot be empty or whitespace.",
                nameof(iconKey));
        }

        ItemId = itemId;
        Title = title;
        IconKey = iconKey;
    }

    /// <summary>
    ///     获取要更新的条目。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取请求的标题；为 null 时恢复身份默认值。
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     获取请求的图标键；为 null 时清除。
    /// </summary>
    public string? IconKey { get; }
}
