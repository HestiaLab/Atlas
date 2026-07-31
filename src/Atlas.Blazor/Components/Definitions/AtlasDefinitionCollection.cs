using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

internal sealed record AtlasDefinitionParent(LayoutNodeId NodeId);

internal sealed record AtlasSplitDescriptor(
    LayoutNodeId Id,
    SplitAxis Axis,
    SplitBasis Basis,
    SplitConstraints Constraints,
    LayoutNodeId? ParentId,
    int Sequence);

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

internal sealed record AtlasItemDescriptor(
    DockItem Item,
    LayoutNodeId GroupId,
    int Sequence);

internal sealed class AtlasDefinitionCollector
{
    private readonly Dictionary<LayoutNodeId, object> nodes = new();
    private readonly Dictionary<DockItemId, AtlasItemDescriptor> items = new();
    private int sequence;
    private bool duplicate;
    private bool invalid;

    internal void AddSplit(
        LayoutNodeId id,
        SplitAxis axis,
        SplitBasis basis,
        SplitConstraints constraints,
        LayoutNodeId? parentId)
    {
        AddNode(
            id,
            new AtlasSplitDescriptor(
                id,
                axis,
                basis,
                constraints,
                parentId,
                sequence++));
    }

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

    internal void AddItem(DockItem item, LayoutNodeId groupId)
    {
        if (!items.TryAdd(item.Id, new AtlasItemDescriptor(item, groupId, sequence++)))
        {
            duplicate = true;
        }
    }

    internal bool TryBuild(
        WorkspaceId workspaceId,
        out AtlasWorkspaceDefinition? definition)
    {
        definition = null;
        if (workspaceId == default || duplicate || invalid)
        {
            return false;
        }

        var roots = nodes.Values
            .Where(NodeHasNoParent)
            .OrderBy(NodeSequence)
            .ToArray();
        if (roots.Length > 1)
        {
            return false;
        }

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
                    group.LogicalRegion!.Value,
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
        var sides = groups
            .Where(group => group.ItemIds.Count != 0)
            .Select(group => SideFor(group.LogicalRegion!.Value))
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

    internal static LogicalSide SideFor(LogicalRegion region)
    {
        return region is LogicalRegion.InlineStartUpper
            or LogicalRegion.InlineStartLower
            or LogicalRegion.BlockEndInlineStart
            ? LogicalSide.InlineStart
            : LogicalSide.InlineEnd;
    }
}

/// <summary>Declares a two-child split node.</summary>
public sealed class AtlasSplitDefinition : ComponentBase
{
    /// <summary>Gets or sets the required identity parameter; changing it is remove plus add.</summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>Gets or sets the definition axis; the default is inline children.</summary>
    [Parameter]
    public SplitAxis Axis { get; set; } = SplitAxis.InlineChildren;

    /// <summary>Gets or sets the definition sizing basis; the default is a 0.5 proportion.</summary>
    [Parameter]
    public SplitBasis Basis { get; set; } = SplitBasis.Proportional(0.5d);

    /// <summary>Gets or sets definition constraints; the default has no finite maxima.</summary>
    [Parameter]
    public SplitConstraints Constraints { get; set; }

    /// <summary>Gets or sets the required definition children; changes reconcile atomically.</summary>
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
            Collector.AddSplit(Id, Axis, Basis, Constraints, Parent?.NodeId);
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

/// <summary>Declares a tool group node.</summary>
public sealed class AtlasToolGroupDefinition : ComponentBase
{
    /// <summary>Gets or sets the required identity parameter; changing it is remove plus add.</summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>Gets or sets the required definition logical toolbar region.</summary>
    [Parameter]
    [EditorRequired]
    public LogicalRegion LogicalRegion { get; set; }

    /// <summary>Gets or sets the definition retention policy; the default is persistent.</summary>
    [Parameter]
    public GroupRetentionPolicy RetentionPolicy { get; set; } = GroupRetentionPolicy.Persistent;

    /// <summary>Gets or sets definition visibility; the default is expanded.</summary>
    [Parameter]
    public GroupVisibility Visibility { get; set; } = GroupVisibility.Expanded;

    /// <summary>Gets or sets the definition selection; the default selects the first item.</summary>
    [Parameter]
    public DockItemId? SelectedItemId { get; set; }

    /// <summary>Gets or sets definition children; the default is an empty persistent group.</summary>
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

/// <summary>Declares a document group node.</summary>
public sealed class AtlasDocumentGroupDefinition : ComponentBase
{
    /// <summary>Gets or sets the required identity parameter; changing it is remove plus add.</summary>
    [Parameter]
    [EditorRequired]
    public LayoutNodeId Id { get; set; }

    /// <summary>Gets or sets the definition retention policy; the default is persistent.</summary>
    [Parameter]
    public GroupRetentionPolicy RetentionPolicy { get; set; } = GroupRetentionPolicy.Persistent;

    /// <summary>Gets or sets the definition overflow mode; the default is scroll.</summary>
    [Parameter]
    public EditorOverflowMode OverflowMode { get; set; } = EditorOverflowMode.Scroll;

    /// <summary>Gets or sets the definition activation policy; the default is adjacent.</summary>
    [Parameter]
    public EditorActivationPolicy ActivationPolicy { get; set; } = EditorActivationPolicy.Adjacent;

    /// <summary>Gets or sets the definition selection; the default selects the first item.</summary>
    [Parameter]
    public DockItemId? SelectedItemId { get; set; }

    /// <summary>Gets or sets definition children; the default is an empty persistent group.</summary>
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

/// <summary>Declares a tool item.</summary>
public sealed class AtlasToolItemDefinition : ComponentBase
{
    /// <summary>Gets or sets the required identity parameter; changing it is remove plus add.</summary>
    [Parameter]
    [EditorRequired]
    public DockItemId Id { get; set; }

    /// <summary>Gets or sets the required definition content-provider kind.</summary>
    [Parameter]
    [EditorRequired]
    public string ContentKind { get; set; } = string.Empty;

    /// <summary>Gets or sets the required definition business-resource identity.</summary>
    [Parameter]
    [EditorRequired]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the definition title; null derives the Core default title.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Gets or sets the definition icon identity; the default is null.</summary>
    [Parameter]
    public string? IconKey { get; set; }

    /// <summary>Gets or sets definition capabilities; the default is the Tool preset.</summary>
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

/// <summary>Declares a document item.</summary>
public sealed class AtlasDocumentItemDefinition : ComponentBase
{
    /// <summary>Gets or sets the required identity parameter; changing it is remove plus add.</summary>
    [Parameter]
    [EditorRequired]
    public DockItemId Id { get; set; }

    /// <summary>Gets or sets the required definition content-provider kind.</summary>
    [Parameter]
    [EditorRequired]
    public string ContentKind { get; set; } = string.Empty;

    /// <summary>Gets or sets the required definition business-resource identity.</summary>
    [Parameter]
    [EditorRequired]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the definition title; null derives the Core default title.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Gets or sets the definition icon identity; the default is null.</summary>
    [Parameter]
    public string? IconKey { get; set; }

    /// <summary>Gets or sets definition capabilities; the default is the Document preset.</summary>
    [Parameter]
    public DockItemCapabilities Capabilities { get; set; } = DockItemCapabilityPresets.Document;

    /// <summary>Gets or sets definition pin state; the default is false.</summary>
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
