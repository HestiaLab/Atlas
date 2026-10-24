using Atlas.Core.Items;
using Atlas.Core.Placement;

namespace Atlas.Core.Layout;

/// <summary>
///     布局树中的叶节点：承载一组有序条目。文档 Group 由文档行为状态管理且
///     不能折叠；工具 Group 必须关联到语义工具栏区域。
/// </summary>
public sealed class GroupNode : LayoutNode
{
    /// <summary>
    ///     构造 Group 节点并校验组合约束：文档 Group 不能折叠；工具 Group 不能
    ///     携带编辑器状态，且必须声明逻辑区域；非空条目列表必须有一个属于该
    ///     Group 的选中项；空 Group 只允许 Persistent 保留策略。
    /// </summary>
    /// <param name="id">工作区范围内的节点标识符。</param>
    /// <param name="kind">Group 接受的条目种类。</param>
    /// <param name="retentionPolicy">空 Group 的保留策略。</param>
    /// <param name="visibility">已提交的 Group 可见性。</param>
    /// <param name="itemIds">有序的条目标识符。</param>
    /// <param name="selectedItemId">选中条目标识符；空 Group 为 null。</param>
    /// <param name="editorState">仅由文档 Group 携带的行为状态；省略时使用默认值。</param>
    /// <param name="toolbarRegion">仅工具 Group 需要的语义工具栏区域。</param>
    public GroupNode(
        LayoutNodeId id,
        DockItemKind kind,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        IEnumerable<DockItemId> itemIds,
        DockItemId? selectedItemId,
        EditorGroupState? editorState = null,
        LogicalRegion? toolbarRegion = null)
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
            if (toolbarRegion is null
                || toolbarRegion.Value < LogicalRegion.InlineStartUpper
                || toolbarRegion.Value > LogicalRegion.BlockEndInlineEnd)
            {
                throw new ArgumentException(
                    "A tool group must identify a known logical region.",
                    nameof(toolbarRegion));
            }
        }
        else if (toolbarRegion is not null)
        {
            throw new ArgumentException(
                "A document group cannot identify a toolbar logical region.",
                nameof(toolbarRegion));
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
        ToolbarRegion = toolbarRegion;
    }

    /// <summary>
    ///     Group 接受的条目种类。
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     最后一个条目离开 Group 后的处理策略。
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }

    /// <summary>
    ///     已提交的 Group 可见性。
    /// </summary>
    public GroupVisibility Visibility { get; }

    /// <summary>
    ///     按显示顺序排列的条目标识符（只读）。
    /// </summary>
    public IReadOnlyList<DockItemId> ItemIds { get; }

    /// <summary>
    ///     当前选中条目的标识符；空 Group 为 null，非空 Group 始终有一个选中项
    ///     （构造时校验）。
    /// </summary>
    public DockItemId? SelectedItemId { get; }

    /// <summary>
    ///     文档 Group 的编辑器行为状态；工具 Group 始终为
    ///     <see langword="null"/>（构造时校验）。
    /// </summary>
    public EditorGroupState? EditorState { get; }

    /// <summary>
    ///     工具 Group 关联的语义工具栏区域；文档 Group 始终为
    ///     <see langword="null"/>。
    /// </summary>
    public LogicalRegion? ToolbarRegion { get; }

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
