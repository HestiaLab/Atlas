using Atlas.Core.Items;
using Atlas.Core.Placement;

namespace Atlas.Core.Layout;

/// <summary>
///     布局树中的叶子节点：承载一组有序项。
///     文档组受文档行为状态约束且不可折叠；工具组必须挂靠语义工具栏区域。
/// </summary>
public sealed class GroupNode : LayoutNode
{
    /// <summary>
    ///     构造组节点并校验组合约束：文档组不可折叠、工具组不可携带编辑器状态且必须声明逻辑区域、
    ///     项列表非空时必须有选中项且选中项属于该组、空组只允许 Persistent 保留策略。
    /// </summary>
    /// <param name="id">工作区作用域内的节点标识。</param>
    /// <param name="kind">组接受的项种类。</param>
    /// <param name="retentionPolicy">空组保留策略。</param>
    /// <param name="visibility">已提交的组可见性。</param>
    /// <param name="itemIds">有序的项标识。</param>
    /// <param name="selectedItemId">选中的项标识；空组时为 null。</param>
    /// <param name="editorState">仅文档组可携带的行为状态；缺省时使用默认值。</param>
    /// <param name="logicalRegion">仅工具组必须携带的语义工具栏区域。</param>
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
    ///     组接受的项种类。
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     组内最后一项离开后的处理策略。
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }

    /// <summary>
    ///     已提交的组可见性。
    /// </summary>
    public GroupVisibility Visibility { get; }

    /// <summary>
    ///     按显示顺序排列的项标识（只读）。
    /// </summary>
    public IReadOnlyList<DockItemId> ItemIds { get; }

    /// <summary>
    ///     当前选中项的标识；空组为 null，非空组必有一个选中项（构造校验）。
    /// </summary>
    public DockItemId? SelectedItemId { get; }

    /// <summary>
    ///     文档组的编辑器行为状态；工具组恒为 <see langword="null"/>（构造校验）。
    /// </summary>
    public EditorGroupState? EditorState { get; }

    /// <summary>
    ///     工具组挂靠的语义工具栏区域；文档组恒为 <see langword="null"/>。
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
