namespace Atlas.Core.Operations;

internal sealed class UpdateItemPresentationOperation : LayoutOperation
{
    internal UpdateItemPresentationOperation(
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

    internal DockItemId ItemId { get; }

    internal string? Title { get; }

    internal string? IconKey { get; }
}
