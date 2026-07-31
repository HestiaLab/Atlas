using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>Projects one isolated Atlas workspace into a Blazor render tree.</summary>
public sealed class AtlasWorkspaceHost : ComponentBase, IAsyncDisposable
{
    private readonly string hostId = $"atlas-{Guid.NewGuid():N}";
    private AtlasDefinitionCollector collector = new();
    private readonly AtlasFocusCoordinator focusCoordinator = new();
    private readonly AtlasContentRegistry emptyRegistry =
        new(Array.Empty<AtlasContentRegistration>());
    private IAtlasWorkspace? activeWorkspace;
    private bool activeWorkspaceDeclarative;
    private bool ownsActiveWorkspace;
    private AtlasWorkspaceDefinition? previousDeclaration;
    private HashSet<DockItemId> declaredItemIds = new();
    private string? processedFingerprint;
    private AtlasHostError? hostError;
    private bool declarativeMode;
    private bool disposed;

    /// <summary>
    /// Gets or sets the programmatic authoritative workspace. Identity parameter; changing it
    /// removes the old binding and adds the new binding. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public IAtlasWorkspace? Workspace { get; set; }

    /// <summary>
    /// Gets or sets the declarative workspace identity. Identity parameter; changing it creates
    /// a new host workspace. Required in declarative mode.
    /// </summary>
    [Parameter]
    public WorkspaceId? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets declarative create-time workspace options. Definition parameter used only
    /// when an identity is materialized; the default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public AtlasWorkspaceOptions? WorkspaceOptions { get; set; }

    /// <summary>
    /// Gets or sets the transient lifecycle ownership option. Runtime changes transfer disposal
    /// responsibility without changing workspace state. The default is <see cref="WorkspaceOwnership.Automatic"/>.
    /// </summary>
    [Parameter]
    public WorkspaceOwnership WorkspaceOwnership { get; set; } = WorkspaceOwnership.Automatic;

    /// <summary>
    /// Gets or sets declarative child definitions. Definition parameter; each changed render
    /// batch is reconciled atomically. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets fixed top-toolbar presentation. Presentation parameter; runtime changes do
    /// not enter workspace history. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? TopToolbarContent { get; set; }

    /// <summary>
    /// Gets or sets fixed status-bar presentation. Presentation parameter; runtime changes do
    /// not enter workspace history. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? StatusBarContent { get; set; }

    /// <summary>
    /// Gets or sets the application content dispatcher. Template parameter; runtime changes only
    /// invalidate projection. The default is <see langword="null"/>.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasContentContext>? ContentTemplate { get; set; }

    /// <summary>
    /// Gets or sets the tool-group empty-state template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? ToolEmptyTemplate { get; set; }

    /// <summary>
    /// Gets or sets the document-group empty-state template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? DocumentEmptyTemplate { get; set; }

    /// <summary>
    /// Gets or sets the tool header template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? ToolHeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets the document header template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? DocumentHeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets the safe host error template. Template parameter; the default is
    /// <see langword="null"/> and runtime changes only invalidate projection.
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasHostError>? ErrorTemplate { get; set; }

    /// <summary>
    /// Gets or sets the safe host diagnostic callback. Callback parameter; it runs outside Core
    /// workspace locks and may change at runtime.
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    [Inject]
    private IAtlasWorkspaceFactory? Factory { get; set; }

    [Inject]
    private AtlasContentRegistry? Registry { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ThrowIfDisposed();
        ValidateOwnership();
        var hasDeclarativeInput = WorkspaceId is not null || ChildContent is not null;
        if (Workspace is not null && hasDeclarativeInput)
        {
            declarativeMode = false;
            await SetErrorAsync(
                new AtlasHostError(
                    AtlasHostErrorCode.InitializationModeConflict,
                    "ATLAS-HOST-MODE-CONFLICT",
                    "Atlas.Host.InitializationModeConflict",
                    Workspace.Id));
            await UnbindWorkspaceAsync(disposeOwned: true);
            return;
        }

        hostError = null;
        declarativeMode = Workspace is null;
        if (!declarativeMode)
        {
            await BindWorkspaceAsync(
                Workspace!,
                declarative: false,
                ShouldOwnWorkspace(declarative: false));
            return;
        }

        if (WorkspaceId is null || WorkspaceId == default)
        {
            await SetErrorAsync(DefinitionError(null));
            if (!activeWorkspaceDeclarative)
            {
                await UnbindWorkspaceAsync(disposeOwned: true);
            }

            return;
        }

        if (activeWorkspaceDeclarative && activeWorkspace?.Id != WorkspaceId.Value)
        {
            await UnbindWorkspaceAsync(disposeOwned: true);
            ResetDeclarationLedger();
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var sequence = 0;
        if (declarativeMode && WorkspaceId is not null)
        {
            collector = new AtlasDefinitionCollector();
            builder.OpenComponent<CascadingValue<AtlasDefinitionCollector>>(sequence++);
            builder.SetKey(collector);
            builder.AddAttribute(sequence++, "Value", collector);
            builder.AddAttribute(sequence++, "ChildContent", ChildContent);
            builder.CloseComponent();
        }

        if (activeWorkspace is null)
        {
            RenderHostError(builder, ref sequence);
            return;
        }

        var snapshot = activeWorkspace.Current;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-workspace");
        builder.AddAttribute(sequence++, "data-atlas-host-id", hostId);
        builder.AddAttribute(sequence++, "data-atlas-protocol-version", "1");
        builder.AddAttribute(sequence++, "data-atlas-workspace-id", activeWorkspace.Id.Value);
        builder.AddAttribute(sequence++, "data-atlas-direction", "inherit");
        builder.AddAttribute(sequence++, "data-atlas-reduced-motion", "system");

        RenderFixedSlot(builder, ref sequence, "top-toolbar", TopToolbarContent);
        RenderHostError(builder, ref sequence);

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-work-area");
        RenderToolBar(builder, ref sequence, snapshot, LogicalSide.InlineStart);
        if (snapshot.RootNodeId is null)
        {
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "atlas-v2-workspace-empty");
            builder.AddContent(sequence++, "No layout content");
            builder.CloseElement();
        }
        else
        {
            RenderNode(builder, ref sequence, snapshot, snapshot.RootNodeId.Value);
        }

        RenderToolBar(builder, ref sequence, snapshot, LogicalSide.InlineEnd);
        builder.CloseElement();
        RenderFixedSlot(builder, ref sequence, "status-bar", StatusBarContent);
        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!declarativeMode || disposed || WorkspaceId is null)
        {
            return;
        }

        if (!collector.TryBuild(WorkspaceId.Value, out var declaration)
            || declaration is null)
        {
            var fingerprint = "invalid";
            if (!string.Equals(processedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                processedFingerprint = fingerprint;
                await ReportErrorAsync(DefinitionError(WorkspaceId));
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        var currentFingerprint = Fingerprint(declaration);
        if (string.Equals(processedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        processedFingerprint = currentFingerprint;
        if (activeWorkspace is null)
        {
            var result = (Factory ?? new AtlasWorkspaceFactory()).Create(
                declaration,
                WorkspaceOptions);
            if (!result.Succeeded)
            {
                await ReportErrorAsync(DefinitionError(WorkspaceId));
            }
            else
            {
                await BindWorkspaceAsync(
                    result.Value!,
                    declarative: true,
                    ShouldOwnWorkspace(declarative: true));
                RememberDeclaration(declaration);
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!TryMergeRuntimeItems(declaration, out var merged))
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reconcile = await activeWorkspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                merged!,
                activeWorkspace.Current.Revision));
        if (reconcile.Status == WorkspaceResultStatus.Failure)
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
        }
        else
        {
            hostError = null;
            RememberDeclaration(declaration);
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        focusCoordinator.Clear();
        var workspace = activeWorkspace;
        var disposeWorkspace = ownsActiveWorkspace;
        UnbindWorkspace();
        if (workspace is not null && disposeWorkspace)
        {
            await workspace.DisposeAsync();
        }
    }

    private void RenderNode(
        RenderTreeBuilder builder,
        ref int sequence,
        WorkspaceSnapshot snapshot,
        LayoutNodeId nodeId)
    {
        var node = snapshot.Nodes[nodeId];
        if (node is SplitNode split)
        {
            builder.OpenElement(sequence++, "div");
            builder.SetKey(split.Id);
            builder.AddAttribute(sequence++, "class", SplitClass(split.Axis));
            builder.AddAttribute(sequence++, "data-atlas-node-id", split.Id.Value);
            builder.AddAttribute(sequence++, "data-atlas-node-kind", "split");
            builder.AddAttribute(sequence++, "data-atlas-split-axis", split.Axis.ToString());
            builder.AddAttribute(sequence++, "data-atlas-split-locked", "false");
            builder.AddAttribute(sequence++, "style", SplitStyle(split.Basis));
            RenderNode(builder, ref sequence, snapshot, split.FirstId);
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "atlas-v2-splitter");
            builder.AddAttribute(sequence++, "data-atlas-split-id", split.Id.Value);
            builder.CloseElement();
            RenderNode(builder, ref sequence, snapshot, split.SecondId);
            builder.CloseElement();
            return;
        }

        var group = (GroupNode)node;
        var items = group.ItemIds.Select(itemId => snapshot.Items[itemId]).ToArray();
        builder.OpenComponent<AtlasGroupView>(sequence++);
        builder.SetKey(group.Id);
        builder.AddAttribute(sequence++, "Workspace", activeWorkspace);
        builder.AddAttribute(sequence++, "Snapshot", snapshot);
        builder.AddAttribute(sequence++, "Group", group);
        builder.AddAttribute(sequence++, "Items", items);
        builder.AddAttribute(
            sequence++,
            "Registry",
            Registry ?? emptyRegistry);
        builder.AddAttribute(sequence++, "FocusCoordinator", focusCoordinator);
        builder.AddAttribute(sequence++, "ContentTemplate", ContentTemplate);
        builder.AddAttribute(
            sequence++,
            "EmptyTemplate",
            group.Kind == DockItemKind.Tool ? ToolEmptyTemplate : DocumentEmptyTemplate);
        builder.AddAttribute(
            sequence++,
            "HeaderTemplate",
            group.Kind == DockItemKind.Tool ? ToolHeaderTemplate : DocumentHeaderTemplate);
        builder.AddAttribute(sequence++, "OnError", OnError);
        builder.CloseComponent();
    }

    private void RenderToolBar(
        RenderTreeBuilder builder,
        ref int sequence,
        WorkspaceSnapshot snapshot,
        LogicalSide side)
    {
        if (snapshot.ToolBars.TryGetValue(side, out var toolBar)
            && toolBar.Visibility == ToolBarVisibility.Collapsed)
        {
            return;
        }

        var regions = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool
                && group.LogicalRegion is not null
                && AtlasDefinitionCollector.SideFor(group.LogicalRegion.Value) == side)
            .OrderBy(group => group.LogicalRegion)
            .Select(
                group => new
                {
                    Group = group,
                    Items = OrderedItems(snapshot, group).ToArray(),
                })
            .Where(region => region.Items.Length != 0)
            .ToArray();
        if (regions.Length == 0)
        {
            return;
        }

        builder.OpenElement(sequence++, "nav");
        builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar");
        builder.AddAttribute(sequence++, "role", "tablist");
        builder.AddAttribute(sequence++, "aria-orientation", "vertical");
        builder.AddAttribute(
            sequence++,
            "data-atlas-toolbar-side",
            side == LogicalSide.InlineStart ? "inline-start" : "inline-end");
        for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
        {
            var region = regions[regionIndex];
            if (regionIndex != 0)
            {
                builder.OpenElement(sequence++, "div");
                builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar-divider");
                builder.AddAttribute(sequence++, "role", "separator");
                builder.CloseElement();
            }

            builder.OpenElement(sequence++, "section");
            builder.SetKey(region.Group.Id);
            builder.AddAttribute(sequence++, "class", "atlas-v2-toolbar-region");
            builder.AddAttribute(
                sequence++,
                "data-atlas-toolbar-region",
                region.Group.LogicalRegion!.Value.ToString());
            foreach (var item in region.Items)
            {
                var isActive = region.Group.Visibility == GroupVisibility.Expanded
                    && region.Group.SelectedItemId == item.Id;
                builder.OpenElement(sequence++, "button");
                builder.SetKey(item.Id);
                builder.AddAttribute(sequence++, "type", "button");
                builder.AddAttribute(sequence++, "role", "tab");
                builder.AddAttribute(sequence++, "aria-label", item.Title);
                builder.AddAttribute(sequence++, "aria-selected", isActive);
                builder.AddAttribute(sequence++, "data-atlas-item-id", item.Id.Value);
                builder.AddAttribute(sequence++, "data-atlas-item-kind", "tool");
                builder.AddAttribute(
                    sequence++,
                    "data-atlas-command-capabilities",
                    ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
                builder.AddAttribute(
                    sequence++,
                    "onclick",
                    EventCallback.Factory.Create(
                        this,
                        () => ToggleToolAsync(region.Group, item.Id, isActive)));
                builder.AddContent(sequence++, item.Title);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderFixedSlot(
        RenderTreeBuilder builder,
        ref int sequence,
        string slot,
        RenderFragment? content)
    {
        if (content is null)
        {
            return;
        }

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", $"atlas-v2-{slot}");
        builder.AddAttribute(sequence++, "data-atlas-fixed-slot", slot);
        builder.AddContent(sequence++, content);
        builder.CloseElement();
    }

    private void RenderHostError(RenderTreeBuilder builder, ref int sequence)
    {
        if (hostError is null)
        {
            return;
        }

        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-host-error");
        builder.AddAttribute(sequence++, "data-atlas-error-code", hostError.Code.ToString());
        if (ErrorTemplate is not null)
        {
            builder.AddContent(sequence++, ErrorTemplate(hostError));
        }
        else
        {
            builder.AddContent(sequence++, "Atlas could not apply the requested layout.");
        }

        builder.CloseElement();
    }

    private static IEnumerable<DockItem> OrderedItems(
        WorkspaceSnapshot snapshot,
        GroupNode group)
    {
        var ids = group.LogicalRegion is LogicalRegion region
            && snapshot.ToolBarOrders.TryGetValue(region, out var order)
            ? order.ItemIds
            : group.ItemIds;
        return ids.Select(itemId => snapshot.Items[itemId]);
    }

    private async Task ToggleToolAsync(
        GroupNode group,
        DockItemId itemId,
        bool isActive)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        LayoutOperation operation = isActive
            ? new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed)
            : new SelectItemOperation(itemId);
        await activeWorkspace.ExecuteAsync(operation);
    }

    private async Task BindWorkspaceAsync(
        IAtlasWorkspace workspace,
        bool declarative,
        bool ownsWorkspace)
    {
        if (ReferenceEquals(activeWorkspace, workspace))
        {
            activeWorkspaceDeclarative = declarative;
            ownsActiveWorkspace = ownsWorkspace;
            return;
        }

        await UnbindWorkspaceAsync(disposeOwned: true);
        activeWorkspace = workspace;
        activeWorkspaceDeclarative = declarative;
        ownsActiveWorkspace = ownsWorkspace;
        activeWorkspace.Changed += OnWorkspaceChanged;
        activeWorkspace.DomainEvents.ItemActivated += OnItemActivated;
    }

    private void UnbindWorkspace()
    {
        if (activeWorkspace is null)
        {
            return;
        }

        activeWorkspace.Changed -= OnWorkspaceChanged;
        activeWorkspace.DomainEvents.ItemActivated -= OnItemActivated;
        activeWorkspace = null;
        activeWorkspaceDeclarative = false;
        ownsActiveWorkspace = false;
    }

    private async Task UnbindWorkspaceAsync(bool disposeOwned)
    {
        var workspace = activeWorkspace;
        var shouldDispose = disposeOwned && ownsActiveWorkspace;
        UnbindWorkspace();
        if (workspace is not null && shouldDispose)
        {
            await workspace.DisposeAsync();
        }
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private void OnItemActivated(object? sender, ItemActivatedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        focusCoordinator.Request(args.ItemId, args.AfterRevision);
        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private bool TryMergeRuntimeItems(
        AtlasWorkspaceDefinition declaration,
        out AtlasWorkspaceDefinition? merged)
    {
        merged = null;
        if (activeWorkspace is null || previousDeclaration is null)
        {
            merged = declaration;
            return true;
        }

        var current = activeWorkspace.Current;
        var runtimeIds = current.Items.Keys
            .Where(itemId => !declaredItemIds.Contains(itemId))
            .ToHashSet();
        var placedRuntimeIds = current.Nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .Concat(
                current.AutoHideStrips.Values.SelectMany(
                    strip => strip.Entries.Select(entry => entry.ItemId)))
            .Where(runtimeIds.Contains)
            .Distinct()
            .ToArray();
        if (placedRuntimeIds.Length != runtimeIds.Count)
        {
            return false;
        }

        var runtimeItems = placedRuntimeIds
            .Select(itemId => current.Items[itemId])
            .ToArray();
        if (runtimeItems.Any(item => declaration.Items.Any(candidate => candidate.Id == item.Id)))
        {
            return false;
        }

        var runtimeByGroup = new Dictionary<LayoutNodeId, List<DockItem>>();
        foreach (var currentGroup in current.Nodes.Values.OfType<GroupNode>())
        {
            var groupRuntimeItems = currentGroup.ItemIds
                .Where(runtimeIds.Contains)
                .Select(itemId => current.Items[itemId])
                .ToList();
            if (groupRuntimeItems.Count == 0)
            {
                continue;
            }

            if (!declaration.Nodes.Any(
                node => node is GroupNode target
                    && target.Id == currentGroup.Id
                    && target.Kind == currentGroup.Kind))
            {
                return false;
            }

            runtimeByGroup.Add(currentGroup.Id, groupRuntimeItems);
        }

        var nodes = declaration.Nodes.Select(
            node =>
            {
                if (node is not GroupNode group
                    || !runtimeByGroup.TryGetValue(group.Id, out var additions))
                {
                    return node;
                }

                var ids = group.ItemIds.Concat(additions.Select(item => item.Id)).ToArray();
                var currentGroup = (GroupNode)current.Nodes[group.Id];
                var selected = currentGroup.SelectedItemId is DockItemId selectedId
                    && additions.Any(item => item.Id == selectedId)
                    ? selectedId
                    : group.SelectedItemId;
                return new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    ids,
                    selected,
                    group.EditorState,
                    group.LogicalRegion);
            }).ToArray();
        var orders = nodes.OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool)
            .Select(
                group =>
                {
                    var declarationOrder = group.ItemIds
                        .Where(itemId => !runtimeIds.Contains(itemId));
                    var runtimeOrder = current.ToolBarOrders.TryGetValue(
                        group.LogicalRegion!.Value,
                        out var currentOrder)
                        ? currentOrder.ItemIds.Where(runtimeIds.Contains)
                        : group.ItemIds.Where(runtimeIds.Contains);
                    return new ToolBarOrderState(
                        group.LogicalRegion.Value,
                        declarationOrder.Concat(runtimeOrder));
                });
        var autoHideStrips = declaration.AutoHideStrips.Concat(
            current.AutoHideStrips.Values
                .Select(
                    strip => new AutoHideStripState(
                        strip.Edge,
                        strip.Entries.Where(entry => runtimeIds.Contains(entry.ItemId))))
                .Where(strip => strip.Entries.Count != 0));
        merged = new AtlasWorkspaceDefinition(
            declaration.WorkspaceId,
            declaration.RootNodeId,
            nodes,
            declaration.Items.Concat(runtimeItems),
            declaration.ToolBars,
            orders,
            autoHideStrips);
        return true;
    }

    private void RememberDeclaration(AtlasWorkspaceDefinition declaration)
    {
        previousDeclaration = declaration;
        declaredItemIds = declaration.Items.Select(item => item.Id).ToHashSet();
    }

    private async Task ReportErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private async Task SetErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private AtlasHostError DefinitionError(WorkspaceId? workspaceId)
    {
        return new AtlasHostError(
            AtlasHostErrorCode.DefinitionInvalid,
            "ATLAS-DEFINITION-INVALID",
            "Atlas.Definition.Invalid",
            workspaceId);
    }

    private bool ShouldOwnWorkspace(bool declarative)
    {
        return WorkspaceOwnership
                == global::Atlas.Blazor.Components.WorkspaceOwnership.Host
            || (WorkspaceOwnership
                    == global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
                && declarative);
    }

    private void ValidateOwnership()
    {
        if (WorkspaceOwnership
            is < global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
            or > global::Atlas.Blazor.Components.WorkspaceOwnership.Host)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WorkspaceOwnership),
                WorkspaceOwnership,
                "Unknown workspace ownership.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasWorkspaceHost));
        }
    }

    private static string Fingerprint(AtlasWorkspaceDefinition definition)
    {
        return string.Join(
            "|",
            new[]
            {
                definition.WorkspaceId.Value,
                definition.RootNodeId?.Value ?? string.Empty,
                string.Join(",", definition.Nodes.Select(NodeFingerprint)),
                string.Join(",", definition.Items.Select(ItemFingerprint)),
            });
    }

    private void ResetDeclarationLedger()
    {
        previousDeclaration = null;
        declaredItemIds.Clear();
        processedFingerprint = null;
    }

    private static string NodeFingerprint(LayoutNode node)
    {
        return node switch
        {
            SplitNode split => $"S:{split.Id}:{split.Axis}:{split.Basis.Kind}:{split.Basis.Value}:{split.FirstId}:{split.SecondId}",
            GroupNode group => $"G:{group.Id}:{group.Kind}:{group.RetentionPolicy}:{group.Visibility}:{group.LogicalRegion}:{group.SelectedItemId}:{string.Join("/", group.ItemIds)}",
            _ => node.Id.Value,
        };
    }

    private static string ItemFingerprint(DockItem item)
    {
        return $"I:{item.Id}:{item.Kind}:{item.Content.Kind}:{item.Content.ResourceId}:{item.Title}:{item.IconKey}:{item.Capabilities}:{item.DocumentState?.IsPinned}";
    }

    private static string SplitClass(SplitAxis axis)
    {
        return axis == SplitAxis.InlineChildren
            ? "atlas-v2-split atlas-v2-split-inline"
            : "atlas-v2-split atlas-v2-split-block";
    }

    private static string SplitStyle(SplitBasis basis)
    {
        string first;
        string second;
        if (basis.Kind == SplitBasisKind.Proportional)
        {
            first = $"minmax(0,{basis.Value.ToString("R", CultureInfo.InvariantCulture)}fr)";
            second =
                $"minmax(0,{(1d - basis.Value).ToString("R", CultureInfo.InvariantCulture)}fr)";
        }
        else if (basis.Anchor == SplitAnchor.First)
        {
            first = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
            second = "minmax(0,1fr)";
        }
        else
        {
            first = "minmax(0,1fr)";
            second = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
        }

        return $"--atlas-first-track:{first};--atlas-second-track:{second}";
    }
}
