namespace Atlas.Core.Operations;

/// <summary>Updates UI-independent title and icon metadata for an item.</summary>
public sealed class UpdateItemPresentationOperation : LayoutOperation
{
    /// <summary>Initializes an item presentation operation.</summary>
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

    /// <summary>Gets the item to update.</summary>
    public DockItemId ItemId { get; }

    /// <summary>Gets the requested title, or null to restore the identity default.</summary>
    public string? Title { get; }

    /// <summary>Gets the requested icon key, or null to clear it.</summary>
    public string? IconKey { get; }
}
