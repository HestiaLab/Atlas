using Atlas.Core.Items;
using Atlas.Core.Placement;

namespace Atlas.Core.Layout;

/// <summary>
///     A leaf node in the layout tree: hosts an ordered set of items.
///     Document groups are governed by document behaviour state and cannot be collapsed; tool groups must
///     be attached to a semantic tool bar region.
/// </summary>
public sealed class GroupNode : LayoutNode
{
    /// <summary>
    ///     Constructs a group node and validates the combination constraints: document groups cannot be
    ///     collapsed, tool groups cannot carry editor state and must declare a logical region, a non-empty
    ///     item list must have a selected item that belongs to the group, and an empty group only allows the
    ///     Persistent retention policy.
    /// </summary>
    /// <param name="id">The node identifier within the workspace scope.</param>
    /// <param name="kind">The item kind accepted by the group.</param>
    /// <param name="retentionPolicy">The retention policy for an empty group.</param>
    /// <param name="visibility">The committed group visibility.</param>
    /// <param name="itemIds">The ordered item identifiers.</param>
    /// <param name="selectedItemId">The selected item identifier; null for an empty group.</param>
    /// <param name="editorState">Behaviour state carried by document groups only; defaults are used when omitted.</param>
    /// <param name="logicalRegion">The semantic tool bar region required for tool groups only.</param>
    public GroupNode(
        LayoutNodeId id,
        DockItemKind kind,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        IEnumerable<DockItemId> itemIds,
        DockItemId? selectedItemId,
        EditorGroupState? editorState = null,
        LogicalRegion? logicalRegion = null)
        : base(id)
    {
        ValidateEnums(kind, retentionPolicy, visibility);

        if (kind == DockItemKind.Document && visibility == GroupVisibility.Collapsed)
        {
            throw new ArgumentException(
                "Document groups cannot be collapsed.",
                nameof(visibility));
        }

        if (kind == DockItemKind.Tool && editorState is not null)
        {
            throw new ArgumentException(
                "Tool groups cannot contain editor state.",
                nameof(editorState));
        }

        if (kind == DockItemKind.Tool)
        {
            if (logicalRegion is null
                || logicalRegion.Value < global::Atlas.Core.Placement.LogicalRegion.InlineStartUpper
                || logicalRegion.Value > global::Atlas.Core.Placement.LogicalRegion.BlockEndInlineEnd)
            {
                throw new ArgumentException(
                    "A tool group must identify a known logical region.",
                    nameof(logicalRegion));
            }
        }
        else if (logicalRegion is not null)
        {
            throw new ArgumentException(
                "A document group cannot identify a toolbar logical region.",
                nameof(logicalRegion));
        }

        ArgumentNullException.ThrowIfNull(itemIds);

        var materializedItemIds = itemIds.ToArray();
        ValidateItems(materializedItemIds, retentionPolicy, selectedItemId);

        Kind = kind;
        RetentionPolicy = retentionPolicy;
        Visibility = visibility;
        ItemIds = Array.AsReadOnly(materializedItemIds);
        SelectedItemId = selectedItemId;
        EditorState = kind == DockItemKind.Document
            ? editorState ?? new EditorGroupState()
            : null;
        LogicalRegion = logicalRegion;
    }

    /// <summary>
    ///     The item kind accepted by the group.
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     Policy for what happens after the last item leaves the group.
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }

    /// <summary>
    ///     The committed group visibility.
    /// </summary>
    public GroupVisibility Visibility { get; }

    /// <summary>
    ///     Item identifiers in display order (read-only).
    /// </summary>
    public IReadOnlyList<DockItemId> ItemIds { get; }

    /// <summary>
    ///     The identifier of the currently selected item; null for an empty group, while a non-empty group always has one selected item (validated at construction).
    /// </summary>
    public DockItemId? SelectedItemId { get; }

    /// <summary>
    ///     The editor behaviour state of a document group; always <see langword="null"/> for tool groups (validated at construction).
    /// </summary>
    public EditorGroupState? EditorState { get; }

    /// <summary>
    ///     The semantic tool bar region a tool group is attached to; always <see langword="null"/> for document groups.
    /// </summary>
    public LogicalRegion? LogicalRegion { get; }

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
