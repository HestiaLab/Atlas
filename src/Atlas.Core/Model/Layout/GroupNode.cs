using Atlas.Core.Items;

namespace Atlas.Core.Layout;

/// <summary>
/// Represents an ordered tool or document group in the layout tree.
/// </summary>
public sealed class GroupNode : LayoutNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupNode"/> class.
    /// </summary>
    /// <param name="id">The workspace-scoped node identity.</param>
    /// <param name="kind">The item kind accepted by the group.</param>
    /// <param name="retentionPolicy">The empty-group retention policy.</param>
    /// <param name="visibility">The committed group visibility.</param>
    /// <param name="itemIds">The ordered item identities.</param>
    /// <param name="selectedItemId">The selected item identity, or null for an empty group.</param>
    public GroupNode(
        LayoutNodeId id,
        DockItemKind kind,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        IEnumerable<DockItemId> itemIds,
        DockItemId? selectedItemId)
        : base(id)
    {
        ValidateEnums(kind, retentionPolicy, visibility);

        if (kind == DockItemKind.Document && visibility == GroupVisibility.Collapsed)
        {
            throw new ArgumentException(
                "Document groups cannot be collapsed.",
                nameof(visibility));
        }

        ArgumentNullException.ThrowIfNull(itemIds);

        var materializedItemIds = itemIds.ToArray();
        ValidateItems(materializedItemIds, retentionPolicy, selectedItemId);

        Kind = kind;
        RetentionPolicy = retentionPolicy;
        Visibility = visibility;
        ItemIds = Array.AsReadOnly(materializedItemIds);
        SelectedItemId = selectedItemId;
    }

    /// <summary>
    /// Gets the item kind accepted by the group.
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    /// Gets the empty-group retention policy.
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }

    /// <summary>
    /// Gets the committed group visibility.
    /// </summary>
    public GroupVisibility Visibility { get; }

    /// <summary>
    /// Gets the ordered item identities.
    /// </summary>
    public IReadOnlyList<DockItemId> ItemIds { get; }

    /// <summary>
    /// Gets the selected item identity, or null for an empty group.
    /// </summary>
    public DockItemId? SelectedItemId { get; }

    private static void ValidateEnums(
        DockItemKind kind,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility)
    {
        if (kind is not DockItemKind.Tool and not DockItemKind.Document)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown group kind.");
        }

        if (retentionPolicy is not GroupRetentionPolicy.Persistent
            and not GroupRetentionPolicy.Dynamic)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionPolicy),
                retentionPolicy,
                "Unknown group retention policy.");
        }

        if (visibility is not GroupVisibility.Expanded and not GroupVisibility.Collapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibility),
                visibility,
                "Unknown group visibility.");
        }
    }

    private static void ValidateItems(
        IReadOnlyList<DockItemId> itemIds,
        GroupRetentionPolicy retentionPolicy,
        DockItemId? selectedItemId)
    {
        if (itemIds.Any(itemId => itemId == default))
        {
            throw new ArgumentException(
                "A group cannot contain a default item identifier.",
                nameof(itemIds));
        }

        if (itemIds.Distinct().Count() != itemIds.Count)
        {
            throw new ArgumentException(
                "A group cannot contain duplicate item identifiers.",
                nameof(itemIds));
        }

        if (itemIds.Count == 0)
        {
            if (retentionPolicy == GroupRetentionPolicy.Dynamic)
            {
                throw new ArgumentException(
                    "An empty dynamic group cannot be committed.",
                    nameof(itemIds));
            }

            if (selectedItemId is not null)
            {
                throw new ArgumentException(
                    "An empty group cannot select an item.",
                    nameof(selectedItemId));
            }

            return;
        }

        if (selectedItemId is null || selectedItemId == default)
        {
            throw new ArgumentException(
                "A non-empty group must select one of its items.",
                nameof(selectedItemId));
        }

        if (!itemIds.Contains(selectedItemId.Value))
        {
            throw new ArgumentException(
                "The selected item must belong to the group.",
                nameof(selectedItemId));
        }
    }
}
