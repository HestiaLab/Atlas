using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

/// <summary>
///     级联传递给子定义的父节点标识；子定义据此登记到父节点之下。
/// </summary>
/// <param name="NodeId">父节点的布局标识。</param>
internal sealed record AtlasDefinitionParent(LayoutNodeId NodeId);

/// <summary>
///     一条已收集的分割声明，记录分割的轴、基准、约束与呈现。
/// </summary>
/// <param name="Id">分割节点标识。</param>
/// <param name="Axis">分割轴。</param>
/// <param name="Basis">尺寸基准。</param>
/// <param name="Constraints">尺寸约束。</param>
/// <param name="Presentation">分割条呈现选项。</param>
/// <param name="ParentId">父节点标识；为 null 表示根节点。</param>
/// <param name="Sequence">声明顺序号，由收集顺序分配。</param>
internal sealed record AtlasSplitDescriptor(
    LayoutNodeId Id,
    SplitAxis Axis,
    SplitBasis Basis,
    SplitConstraints Constraints,
    AtlasSplitterPresentation Presentation,
    LayoutNodeId? ParentId,
    int Sequence);

/// <summary>
///     一条已收集的组声明，记录组的种类、保留策略、可见性与选中状态。
/// </summary>
/// <param name="Id">组节点标识。</param>
/// <param name="Kind">组的行为种类。</param>
/// <param name="RetentionPolicy">组保留策略。</param>
/// <param name="Visibility">组可见性。</param>
/// <param name="EditorState">文档组编辑状态；工具组为 null。</param>
/// <param name="LogicalRegion">工具组的逻辑工具栏区域；文档组为 null。</param>
/// <param name="SelectedItemId">显式选中的项；为 null 时构建阶段回退到第一个声明项。</param>
/// <param name="ParentId">父节点标识；为 null 表示根节点。</param>
/// <param name="Sequence">声明顺序号，由收集顺序分配。</param>
internal sealed record AtlasGroupDescriptor(
    LayoutNodeId Id,
    DockItemKind Kind,
    GroupRetentionPolicy RetentionPolicy,
    GroupVisibility Visibility,
    EditorGroupState? EditorState,
    LogicalRegion? LogicalRegion,
    DockItemId? SelectedItemId,
    LayoutNodeId? ParentId,
    int Sequence);

/// <summary>
///     一条已收集的项声明，携带不可变项模型与其所属组。
/// </summary>
/// <param name="Item">声明出的不可变项模型。</param>
/// <param name="GroupId">项所属的组节点标识。</param>
/// <param name="Sequence">声明顺序号，由收集顺序分配。</param>
internal sealed record AtlasItemDescriptor(
    DockItem Item,
    LayoutNodeId GroupId,
    int Sequence);

/// <summary>
///     跨多次参数设置与渲染批次累积声明式工作区定义，
///     构建时按声明顺序物化为不可变工作区定义。
/// </summary>
internal sealed class AtlasDefinitionCollector
{
    // 已收集的分割/组声明，按节点标识索引。
    private readonly Dictionary<LayoutNodeId, object> nodes = new();

    // 已收集的项声明，按项标识索引。
    private readonly Dictionary<DockItemId, AtlasItemDescriptor> items = new();

    // 声明顺序号分配器；物化时保证输出与组件声明顺序一致。
    private int sequence;

    // 重复节点/项声明的锁存失败位。
    private bool duplicate;

    // 显式失效的锁存失败位；置位后构建一律失败。
    private bool invalid;

    /// <summary>
    ///     登记一条分割声明；节点标识重复会使收集进入重复失败状态。
    /// </summary>
    /// <param name="id">分割节点标识。</param>
    /// <param name="axis">分割轴。</param>
    /// <param name="basis">尺寸基准。</param>
    /// <param name="constraints">尺寸约束。</param>
    /// <param name="presentation">分割条呈现选项。</param>
    /// <param name="parentId">父节点标识；为 null 表示根节点。</param>
    internal void AddSplit(
        LayoutNodeId id,
        SplitAxis axis,
        SplitBasis basis,
        SplitConstraints constraints,
        AtlasSplitterPresentation presentation,
        LayoutNodeId? parentId)
    {
        AddNode(
            id,
            new AtlasSplitDescriptor(
                id,
                axis,
                basis,
                constraints,
                presentation,
                parentId,
                sequence++));
    }

    internal IReadOnlyDictionary<LayoutNodeId, AtlasSplitterPresentation>
        GetSplitPresentations()
    {
        return nodes.Values
            .OfType<AtlasSplitDescriptor>()
            .ToDictionary(split => split.Id, split => split.Presentation);
    }

    /// <summary>
    ///     登记一条组声明；节点标识重复会使收集进入重复失败状态。
    /// </summary>
    /// <param name="id">组节点标识。</param>
    /// <param name="kind">组的行为种类。</param>
    /// <param name="retentionPolicy">组保留策略。</param>
    /// <param name="visibility">组可见性。</param>
    /// <param name="editorState">文档组编辑状态；工具组传 null。</param>
    /// <param name="logicalRegion">工具组的逻辑工具栏区域；文档组传 null。</param>
    /// <param name="selectedItemId">显式选中的项；为 null 时构建阶段回退到第一个声明项。</param>
    /// <param name="parentId">父节点标识；为 null 表示根节点。</param>
    internal void AddGroup(
        LayoutNodeId id,
        DockItemKind kind,
        GroupRetentionPolicy retentionPolicy,
        GroupVisibility visibility,
        EditorGroupState? editorState,
        LogicalRegion? logicalRegion,
        DockItemId? selectedItemId,
        LayoutNodeId? parentId)
    {
        AddNode(
            id,
            new AtlasGroupDescriptor(
                id,
                kind,
                retentionPolicy,
                visibility,
                editorState,
                logicalRegion,
                selectedItemId,
                parentId,
                sequence++));
    }

    /// <summary>
    ///     登记一项声明；项标识重复会使收集进入重复失败状态。
    /// </summary>
    /// <param name="item">声明出的不可变项模型。</param>
    /// <param name="groupId">项所属的组节点标识。</param>
    internal void AddItem(DockItem item, LayoutNodeId groupId)
    {
        if (!items.TryAdd(item.Id, new AtlasItemDescriptor(item, groupId, sequence++)))
        {
            duplicate = true;
        }
    }

    /// <summary>
    ///     将收集到的声明物化为不可变工作区定义；
    ///     重复、无效或声明树结构非法（如多个根）时返回失败而不抛出。
    /// </summary>
    /// <param name="workspaceId">定义所属的工作区标识。</param>
    /// <param name="definition">构建成功时输出的定义；失败时为 null。</param>
    /// <returns>
    ///     构建成功返回 <see langword="true"/>；收集重复、无效或声明树非法时返回 <see langword="false"/>。
    /// </returns>
    internal bool TryBuild(
        WorkspaceId workspaceId,
        out AtlasWorkspaceDefinition? definition)
    {
        definition = null;
        // 收集器跨多次参数设置与渲染批次累积声明；重复与无效状态被锁存为失败位，
        // 在构建时统一判定并返回失败，而不是在渲染期间抛出中断整棵渲染树。
        if (workspaceId == default || duplicate || invalid)
        {
            return false;
        }

        // 物化必须遵循声明顺序：Sequence 由组件的渲染顺序分配，
        // 排序保证输出确定，不依赖字典枚举顺序。
        var roots = nodes.Values
            .Where(NodeHasNoParent)
            .OrderBy(NodeSequence)
            .ToArray();
        // 声明树至多一个根：多个根说明定义不合法，直接判定构建失败。
        if (roots.Length > 1)
        {
            return false;
        }

        // 物化阶段会验证树结构（如分割必须恰好有两个子节点）；
        // 结构非法时以构建失败上报，避免异常逃逸到渲染管线。
        try
        {
            var materializedNodes = nodes.Values
                .OrderBy(NodeSequence)
                .Select(MaterializeNode)
                .ToArray();
            var materializedItems = items.Values
                .OrderBy(item => item.Sequence)
                .Select(item => item.Item)
                .ToArray();
            var toolGroups = materializedNodes
                .OfType<GroupNode>()
                .Where(group => group.Kind == DockItemKind.Tool)
                .ToArray();
            var toolBars = BuildToolBars(toolGroups);
            var orders = toolGroups.Select(
                group => new ToolBarOrderState(
                    group.ToolbarRegion!.Value,
                    group.ItemIds));

            definition = new AtlasWorkspaceDefinition(
                workspaceId,
                roots.Length == 0 ? null : NodeId(roots[0]),
                materializedNodes,
                materializedItems,
                toolBars,
                orders);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IEnumerable<ToolBarState> BuildToolBars(
        IEnumerable<GroupNode> groups)
    {
        // 多个工具组可以落在同一逻辑侧，但工具栏模型每侧只有一条；
        // 按侧去重，避免同一侧生成重复的工具栏状态。
        var sides = groups
            .Where(group => group.ItemIds.Count != 0)
            .Select(group => SideFor(group.ToolbarRegion!.Value))
            .Distinct();
        return sides.Select(side => new ToolBarState(side, ToolBarVisibility.Expanded));
    }

    private LayoutNode MaterializeNode(object descriptor)
    {
        return descriptor switch
        {
            AtlasSplitDescriptor split => MaterializeSplit(split),
            AtlasGroupDescriptor group => MaterializeGroup(group),
            _ => throw new InvalidOperationException("Unknown definition descriptor."),
        };
    }

    private SplitNode MaterializeSplit(AtlasSplitDescriptor split)
    {
        var children = nodes.Values
            .Where(node => NodeParent(node) == split.Id)
            .OrderBy(NodeSequence)
            .Select(NodeId)
            .ToArray();
        // 分割在布局模型中恒为二叉树：子节点必须恰为 2 个，且按声明顺序排列；
        // 非法结构抛出的异常由 TryBuild 捕获并转为构建失败。
        if (children.Length != 2)
        {
            throw new ArgumentException("A split declaration must contain exactly two child nodes.");
        }

        return new SplitNode(
            split.Id,
            split.Axis,
            split.Basis,
            split.Constraints,
            children[0],
            children[1]);
    }

    private GroupNode MaterializeGroup(AtlasGroupDescriptor group)
    {
        var groupItems = items.Values
            .Where(item => item.GroupId == group.Id)
            .OrderBy(item => item.Sequence)
            .Select(item => item.Item.Id)
            .ToArray();
        // 组内有项但未显式指定选中项时回退到第一个声明项，
        // 保证默认选中结果确定且与声明顺序一致。
        DockItemId? selected = groupItems.Length == 0
            ? null
            : group.SelectedItemId ?? groupItems[0];
        return new GroupNode(
            group.Id,
            group.Kind,
            group.RetentionPolicy,
            group.Visibility,
            groupItems,
            selected,
            group.EditorState,
            group.LogicalRegion);
    }

    private void AddNode(LayoutNodeId id, object descriptor)
    {
        if (!nodes.TryAdd(id, descriptor))
        {
            duplicate = true;
        }
    }

    /// <summary>
    ///     将收集器标记为无效；后续构建一律失败，宿主保留先前已提交的定义。
    /// </summary>
    internal void Invalidate()
    {
        invalid = true;
    }

    private static bool NodeHasNoParent(object node)
    {
        return NodeParent(node) is null;
    }

    private static LayoutNodeId? NodeParent(object node)
    {
        return node switch
        {
            AtlasSplitDescriptor split => split.ParentId,
            AtlasGroupDescriptor group => group.ParentId,
            _ => null,
        };
    }

    private static int NodeSequence(object node)
    {
        return node switch
        {
            AtlasSplitDescriptor split => split.Sequence,
            AtlasGroupDescriptor group => group.Sequence,
            _ => int.MaxValue,
        };
    }

    private static LayoutNodeId NodeId(object node)
    {
        return node switch
        {
            AtlasSplitDescriptor split => split.Id,
            AtlasGroupDescriptor group => group.Id,
            _ => default,
        };
    }

    /// <summary>
    ///     将逻辑区域归并到其所属的工具栏侧。
    /// </summary>
    /// <param name="region">逻辑区域。</param>
    /// <returns>
    ///     区域所属的逻辑侧。
    /// </returns>
    internal static LogicalSide SideFor(LogicalRegion region)
    {
        return region is LogicalRegion.InlineStartUpper
            or LogicalRegion.InlineStartLower
            or LogicalRegion.BlockEndInlineStart
            ? LogicalSide.InlineStart
            : LogicalSide.InlineEnd;
    }
}

/// <summary>
///     声明包含两个子节点的分割节点。
/// </summary>
public sealed class AtlasSplitDefinition : ComponentBase
{
    /// <summary>
    ///     获取或设置声明必需的唯一标识；更改它相当于先移除再添加。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>
    ///     获取或设置定义中的轴；默认为内联子项。
    /// </summary>
    [Parameter]
    public SplitAxis Axis { get; set; } = SplitAxis.InlineChildren;

    /// <summary>
    ///     获取或设置定义中的尺寸基准；默认为 0.5 比例。
    /// </summary>
    [Parameter]
    public SplitBasis Basis { get; set; } = SplitBasis.Proportional(0.5d);

    /// <summary>
    ///     获取或设置定义中的约束；默认为无有限最大值。
    /// </summary>
    [Parameter]
    public SplitConstraints Constraints { get; set; }

    /// <summary>
    ///     获取或设置是否禁用用户分割条交互。
    ///     呈现参数。
    /// </summary>
    [Parameter]
    public bool IsLocked { get; set; }

    /// <summary>
    ///     获取或设置分割条可选的安全 CSS 类令牌。
    ///     呈现参数。
    /// </summary>
    [Parameter]
    public string? SplitterClass { get; set; }

    /// <summary>
    ///     获取或设置第一个子项是否可提供折叠操作入口。
    ///     呈现参数。
    /// </summary>
    [Parameter]
    public bool CanCollapseFirst { get; set; }

    /// <summary>
    ///     获取或设置第二个子项是否可提供折叠操作入口。
    ///     呈现参数。
    /// </summary>
    [Parameter]
    public bool CanCollapseSecond { get; set; }

    /// <summary>
    ///     获取或设置必需的子定义；更改会原子地对账。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private AtlasDefinitionCollector Collector { get; set; } = null!;

    [CascadingParameter]
    private AtlasDefinitionParent? Parent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        try
        {
            Collector.AddSplit(
                Id,
                Axis,
                Basis,
                Constraints,
                new AtlasSplitterPresentation(
                    IsLocked,
                    SplitterClass,
                    CanCollapseFirst,
                    CanCollapseSecond),
                Parent?.NodeId);
        }
        catch (ArgumentException)
        {
            Collector.Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<AtlasDefinitionParent>>(0);
        builder.AddAttribute(1, "Value", new AtlasDefinitionParent(Id));
        builder.AddAttribute(2, "ChildContent", ChildContent);
        builder.CloseComponent();
    }
}

/// <summary>
///     声明工具组节点。
/// </summary>
public sealed class AtlasToolGroupDefinition : ComponentBase
{
    /// <summary>
    ///     获取或设置声明必需的唯一标识；更改它相当于先移除再添加。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>
    ///     获取或设置定义中必需的逻辑工具栏区域。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public LogicalRegion LogicalRegion { get; set; }

    /// <summary>
    ///     获取或设置定义中的保留策略；默认为持久。
    /// </summary>
    [Parameter]
    public GroupRetentionPolicy RetentionPolicy { get; set; } = GroupRetentionPolicy.Persistent;

    /// <summary>
    ///     获取或设置定义中的可见性；默认为展开。
    /// </summary>
    [Parameter]
    public GroupVisibility Visibility { get; set; } = GroupVisibility.Expanded;

    /// <summary>
    ///     获取或设置定义中的选中项；默认为选中第一个项。
    /// </summary>
    [Parameter]
    public DockItemId? SelectedItemId { get; set; }

    /// <summary>
    ///     获取或设置定义中的子项；默认为空的持久组。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private AtlasDefinitionCollector Collector { get; set; } = null!;

    [CascadingParameter]
    private AtlasDefinitionParent? Parent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        try
        {
            Collector.AddGroup(
                Id,
                DockItemKind.Tool,
                RetentionPolicy,
                Visibility,
                null,
                LogicalRegion,
                SelectedItemId,
                Parent?.NodeId);
        }
        catch (ArgumentException)
        {
            Collector.Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<AtlasDefinitionParent>>(0);
        builder.AddAttribute(1, "Value", new AtlasDefinitionParent(Id));
        builder.AddAttribute(2, "ChildContent", ChildContent);
        builder.CloseComponent();
    }
}

/// <summary>
///     声明文档组节点。
/// </summary>
public sealed class AtlasDocumentGroupDefinition : ComponentBase
{
    /// <summary>
    ///     获取或设置声明必需的唯一标识；更改它相当于先移除再添加。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>
    ///     获取或设置定义中的保留策略；默认为持久。
    /// </summary>
    [Parameter]
    public GroupRetentionPolicy RetentionPolicy { get; set; } = GroupRetentionPolicy.Persistent;

    /// <summary>
    ///     获取或设置定义中的溢出模式；默认为滚动。
    /// </summary>
    [Parameter]
    public EditorOverflowMode OverflowMode { get; set; } = EditorOverflowMode.Scroll;

    /// <summary>
    ///     获取或设置定义中的激活策略；默认为相邻。
    /// </summary>
    [Parameter]
    public EditorActivationPolicy ActivationPolicy { get; set; } = EditorActivationPolicy.Adjacent;

    /// <summary>
    ///     获取或设置定义中的选中项；默认为选中第一个项。
    /// </summary>
    [Parameter]
    public DockItemId? SelectedItemId { get; set; }

    /// <summary>
    ///     获取或设置定义中的子项；默认为空的持久组。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private AtlasDefinitionCollector Collector { get; set; } = null!;

    [CascadingParameter]
    private AtlasDefinitionParent? Parent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        try
        {
            Collector.AddGroup(
                Id,
                DockItemKind.Document,
                RetentionPolicy,
                GroupVisibility.Expanded,
                new EditorGroupState(OverflowMode, ActivationPolicy),
                null,
                SelectedItemId,
                Parent?.NodeId);
        }
        catch (ArgumentException)
        {
            Collector.Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<AtlasDefinitionParent>>(0);
        builder.AddAttribute(1, "Value", new AtlasDefinitionParent(Id));
        builder.AddAttribute(2, "ChildContent", ChildContent);
        builder.CloseComponent();
    }
}

/// <summary>
///     声明工具项。
/// </summary>
public sealed class AtlasToolItemDefinition : ComponentBase
{
    /// <summary>
    ///     获取或设置声明必需的唯一标识；更改它相当于先移除再添加。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public DockItemId Id { get; set; }

    /// <summary>
    ///     获取或设置定义中必需的内容提供方种类。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string ContentKind { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置定义中必需的业务资源标识。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置定义中的标题；为 null 时派生 Core 默认标题。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///     获取或设置定义中的图标标识；默认为 null。
    /// </summary>
    [Parameter]
    public string? IconKey { get; set; }

    /// <summary>
    ///     获取或设置定义中的能力集；默认为 Tool 预设。
    /// </summary>
    [Parameter]
    public DockItemCapabilities Capabilities { get; set; } = DockItemCapabilityPresets.Tool;

    [CascadingParameter]
    private AtlasDefinitionCollector Collector { get; set; } = null!;

    [CascadingParameter]
    private AtlasDefinitionParent Parent { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        try
        {
            if (Parent is null)
            {
                Collector.Invalidate();
                return;
            }

            Collector.AddItem(
                new DockItem(
                    Id,
                    DockItemKind.Tool,
                    new ContentReference(ContentKind, ResourceId),
                    Capabilities,
                    Title,
                    IconKey),
                Parent.NodeId);
        }
        catch (ArgumentException)
        {
            Collector.Invalidate();
        }
    }
}

/// <summary>
///     声明文档项。
/// </summary>
public sealed class AtlasDocumentItemDefinition : ComponentBase
{
    /// <summary>
    ///     获取或设置声明必需的唯一标识；更改它相当于先移除再添加。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public DockItemId Id { get; set; }

    /// <summary>
    ///     获取或设置定义中必需的内容提供方种类。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string ContentKind { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置定义中必需的业务资源标识。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置定义中的标题；为 null 时派生 Core 默认标题。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///     获取或设置定义中的图标标识；默认为 null。
    /// </summary>
    [Parameter]
    public string? IconKey { get; set; }

    /// <summary>
    ///     获取或设置定义中的能力集；默认为 Document 预设。
    /// </summary>
    [Parameter]
    public DockItemCapabilities Capabilities { get; set; } = DockItemCapabilityPresets.Document;

    /// <summary>
    ///     获取或设置定义中的固定状态；默认为 false。
    /// </summary>
    [Parameter]
    public bool IsPinned { get; set; }

    [CascadingParameter]
    private AtlasDefinitionCollector Collector { get; set; } = null!;

    [CascadingParameter]
    private AtlasDefinitionParent Parent { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        try
        {
            if (Parent is null)
            {
                Collector.Invalidate();
                return;
            }

            Collector.AddItem(
                new DockItem(
                    Id,
                    DockItemKind.Document,
                    new ContentReference(ContentKind, ResourceId),
                    Capabilities,
                    Title,
                    IconKey,
                    new DocumentState(IsPinned, isPreview: false)),
                Parent.NodeId);
        }
        catch (ArgumentException)
        {
            Collector.Invalidate();
        }
    }
}
