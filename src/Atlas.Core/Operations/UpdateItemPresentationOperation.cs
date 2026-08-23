namespace Atlas.Core.Operations;

/// <summary>
///     更新项的呈现元数据：标题与图标标识，与 UI 无关。
/// </summary>
public sealed class UpdateItemPresentationOperation : LayoutOperation
{
    /// <summary>
    ///     初始化项呈现操作。
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
    ///     获取要更新的项。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取请求的标题；为 null 时恢复标识默认值。
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     获取请求的图标标识；为 null 时清除它。
    /// </summary>
    public string? IconKey { get; }
}
