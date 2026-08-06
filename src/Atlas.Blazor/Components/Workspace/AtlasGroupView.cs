using System.Runtime.CompilerServices;
using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Globalization;

namespace Atlas.Blazor.Components;

internal sealed class AtlasGroupView : ComponentBase
{
    private AtlasContentOutlet? selectedContentOutlet;
    private int lastRenderedStamp;
    private int currentStamp;
    private string? reportedMissingContent;
    private readonly List<DockItemId> keptAliveItemIds = new();

    [Parameter]
    public IAtlasWorkspace Workspace { get; set; } = null!;

    [Parameter]
    public WorkspaceSnapshot Snapshot { get; set; } = null!;

    [Parameter]
    public GroupNode Group { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<DockItem> Items { get; set; } = Array.Empty<DockItem>();

    [Parameter]
    public AtlasContentCatalog Catalog { get; set; } = null!;

    [Parameter]
    public AtlasFocusCoordinator FocusCoordinator { get; set; } = null!;

    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? EmptyTemplate { get; set; }

    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? HeaderTemplate { get; set; }

    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    [Parameter]
    public WorkspaceContentLifetimeOptions ContentLifetimeOptions { get; set; } = new();

    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    [Parameter]
    public long Generation { get; set; }

    [Parameter]
    public string HostId { get; set; } = string.Empty;

    [Parameter]
    public IAtlasContentStateScope? StateScope { get; set; }

    protected override void OnParametersSet()
    {
        UpdateLifetimeCache();
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Group));
        foreach (var item in Items)
        {
            hash.Add(RuntimeHelpers.GetHashCode(item));
        }

        hash.Add(EmptyTemplate);
        hash.Add(HeaderTemplate);
        hash.Add(FocusCoordinator.Version);
        hash.Add(ContentLifetimeOptions.Mode);
        hash.Add(ContentLifetimeOptions.MaxCachedItems);
        hash.Add(SandboxedFrameResolver);
        hash.Add(Catalog);
        hash.Add(Generation);
        hash.Add(StateScope);
        foreach (var itemId in keptAliveItemIds)
        {
            hash.Add(itemId);
        }

        currentStamp = hash.ToHashCode();
    }

    protected override bool ShouldRender()
    {
        return lastRenderedStamp != currentStamp;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.SetKey(Group.Id);
        builder.AddAttribute(1, "class", GroupClass());
        builder.AddAttribute(2, "data-atlas-node-id", Group.Id.Value);
        builder.AddAttribute(3, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(4, "data-atlas-group-kind", Group.Kind.ToString().ToLowerInvariant());
        builder.AddAttribute(5, "role", "region");
        if (Group.SelectedItemId is not null)
        {
            var selected = Items.First(item => item.Id == Group.SelectedItemId.Value);
            builder.AddAttribute(
                6,
                "aria-labelledby",
                Group.Kind == DockItemKind.Document
                    ? AtlasDomIdentity.ControlId(HostId, selected.Id, "document-tab")
                    : AtlasDomIdentity.HeadingId(HostId, selected.Id));
        }
        if (Group.LogicalRegion is not null)
        {
            builder.AddAttribute(
                7,
                "data-atlas-logical-region",
                Group.LogicalRegion.Value.ToString());
        }

        if (Items.Count == 0)
        {
            RenderEmpty(builder);
        }
        else
        {
            if (Group.Kind == DockItemKind.Document)
            {
                builder.AddContent(
                    8,
                    (RenderFragment)(headerBuilder =>
                    {
                        RenderTabStrip(headerBuilder);
                    }));
            }
            else
            {
                builder.AddContent(
                    8,
                    (RenderFragment)(headerBuilder =>
                    {
                        RenderToolHeader(headerBuilder);
                    }));
            }

            builder.AddContent(
                9,
                (RenderFragment)(contentBuilder =>
                {
                    RenderContentOutlets(contentBuilder);
                }));
        }

        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        lastRenderedStamp = currentStamp;
        await ReportMissingContentAsync();
        var selected = Group.SelectedItemId;
        if (selected is null
            || !FocusCoordinator.TryClaim(selected.Value, Snapshot.Revision))
        {
            return;
        }

        try
        {
            if (selectedContentOutlet is not null)
            {
                await selectedContentOutlet.FocusAsync();
            }
        }
        catch (Exception)
        {
            if (OnError.HasDelegate)
            {
                await OnError.InvokeAsync(
                    new AtlasHostError(
                        AtlasHostErrorCode.FocusFailed,
                        "ATLAS-FOCUS-FAILED",
                        "Atlas.Focus.Failed",
                        Workspace.Id,
                        selected));
            }
        }
    }

    private async Task ReportMissingContentAsync()
    {
        if (Group.SelectedItemId is null)
        {
            reportedMissingContent = null;
            return;
        }

        var item = SelectedItem();
        var resolution = selectedContentOutlet?.Resolution;
        AtlasHostErrorCode? code = resolution switch
        {
            AtlasContentResolution.Missing => AtlasHostErrorCode.ContentNotRegistered,
            AtlasContentResolution.Conflict => AtlasHostErrorCode.ContentRegistrationConflict,
            _ => null,
        };
        if (code is null)
        {
            reportedMissingContent = null;
            return;
        }

        var identity = $"{item.Id.Value}\0{item.Content.Kind}\0{code}";
        if (string.Equals(reportedMissingContent, identity, StringComparison.Ordinal))
        {
            return;
        }

        reportedMissingContent = identity;
        if (OnError.HasDelegate)
        {
            var isMissing = code.Value == AtlasHostErrorCode.ContentNotRegistered;
            await OnError.InvokeAsync(
                new AtlasHostError(
                    code.Value,
                    isMissing
                        ? "ATLAS-CONTENT-NOT-REGISTERED"
                        : "ATLAS-CONTENT-REGISTRATION-CONFLICT",
                    isMissing
                        ? "Atlas.Content.NotRegistered"
                        : "Atlas.Content.RegistrationConflict",
                    Workspace.Id,
                    item.Id));
        }
    }

    private void RenderEmpty(RenderTreeBuilder builder)
    {
        var context = new AtlasEmptyStateContext(
            Workspace.Id,
            Group.Id,
            Group.Kind,
            Group.RetentionPolicy);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-empty-state");
        if (EmptyTemplate is not null)
        {
            builder.AddContent(2, EmptyTemplate(context));
        }
        else
        {
            builder.AddContent(
                2,
                Group.Kind == DockItemKind.Tool
                    ? "No tool content"
                    : "No document content");
        }

        builder.CloseElement();
    }

    private void RenderTabStrip(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-tab-strip");
        builder.AddAttribute(2, "role", "tablist");
        builder.AddAttribute(3, "aria-orientation", "horizontal");
        builder.AddAttribute(4, "aria-label", $"{Group.Id.Value} documents");
        builder.AddAttribute(5, "data-atlas-group-id", Group.Id.Value);
        for (var itemIndex = 0; itemIndex < Items.Count; itemIndex++)
        {
            var item = Items[itemIndex];
            var selected = item.Id == Group.SelectedItemId;
            // Collection items reuse the same sequence group each iteration
            // and are distinguished by SetKey, so inserting/removing a tab
            // does not shift the diffing sequence of later tabs.
            builder.OpenElement(0, "button");
            builder.SetKey(item.Id);
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "role", "tab");
            builder.AddAttribute(
                3,
                "id",
                AtlasDomIdentity.ControlId(HostId, item.Id, "document-tab"));
            builder.AddAttribute(4, "aria-label", item.Title);
            builder.AddAttribute(
                5,
                "aria-controls",
                AtlasDomIdentity.PanelId(HostId, item.Id));
            builder.AddAttribute(6, "data-atlas-item-id", item.Id.Value);
            builder.AddAttribute(7, "data-atlas-item-kind", "document");
            builder.AddAttribute(8, "data-atlas-drag-kind", "document-tab");
            builder.AddAttribute(9, "data-atlas-group-id", Group.Id.Value);
            builder.AddAttribute(
                10,
                "data-atlas-insertion-index",
                itemIndex.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                11,
                "data-atlas-command-capabilities",
                ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(12, "aria-selected", selected);
            builder.AddAttribute(13, "tabindex", selected ? "0" : "-1");
            builder.AddAttribute(
                14,
                "onclick",
                EventCallback.Factory.Create(this, () => SelectAsync(item.Id)));
            RenderHeader(builder, item, selected);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderToolHeader(RenderTreeBuilder builder)
    {
        var selected = SelectedItem();
        builder.OpenElement(0, "header");
        builder.AddAttribute(1, "class", "atlas-v2-panel-header");
        builder.AddAttribute(2, "role", "heading");
        builder.AddAttribute(3, "aria-level", "2");
        builder.AddAttribute(
            4,
            "id",
            AtlasDomIdentity.HeadingId(HostId, selected.Id));
        builder.AddAttribute(5, "data-atlas-panel-header", "true");
        builder.AddAttribute(6, "data-atlas-drag-kind", "panel-header");
        builder.AddAttribute(7, "data-atlas-item-id", selected.Id.Value);
        builder.AddAttribute(8, "data-atlas-item-kind", "tool");
        builder.AddAttribute(9, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(
            10,
            "data-atlas-command-capabilities",
            ((int)selected.Capabilities).ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(
            11,
            "data-atlas-insertion-index",
            Group.ItemIds.ToList().IndexOf(selected.Id).ToString(CultureInfo.InvariantCulture));
        RenderHeader(builder, selected, isSelected: true);
        builder.CloseElement();
    }

    private void RenderHeader(
        RenderTreeBuilder builder,
        DockItem item,
        bool isSelected)
    {
        if (HeaderTemplate is not null)
        {
            builder.AddContent(
                0,
                HeaderTemplate(new AtlasItemHeaderContext(item, isSelected)));
            return;
        }

        // The title span keeps a fixed sequence (3..6) regardless of whether
        // the icon branch renders, so toggling IconKey does not renumber the
        // title in the diffing tree.
        if (item.IconKey is not null)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "atlas-v2-item-icon");
            builder.AddAttribute(2, "data-atlas-icon", item.IconKey);
            builder.CloseElement();
        }

        builder.OpenElement(3, "span");
        builder.AddAttribute(4, "class", "atlas-v2-item-title");
        builder.AddAttribute(5, "title", item.Title);
        builder.AddContent(6, item.Title);
        builder.CloseElement();
    }

    private void RenderContentOutlets(RenderTreeBuilder builder)
    {
        var selectedId = Group.SelectedItemId
            ?? throw new InvalidOperationException("A non-empty group must select an item.");
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-content-stack");
        foreach (var item in Items)
        {
            if (item.Id != selectedId && !keptAliveItemIds.Contains(item.Id))
            {
                continue;
            }

            RenderContentOutlet(builder, item, item.Id == selectedId);
        }

        builder.CloseElement();
    }

    private void RenderContentOutlet(
        RenderTreeBuilder builder,
        DockItem item,
        bool isSelected)
    {
        var context = new AtlasContentContext(Workspace, item, StateScope);
        var resolution = Catalog.TryResolve(item.Content.Kind, out var descriptor);
        builder.OpenComponent<AtlasContentOutlet>(0);
        builder.SetKey((item.Id, descriptor?.Revision ?? 0));
        builder.AddAttribute(1, "Content", context);
        builder.AddAttribute(2, "Resolution", resolution);
        builder.AddAttribute(3, "Descriptor", descriptor);
        builder.AddAttribute(4, "IsSelected", isSelected);
        builder.AddAttribute(5, "PanelId", AtlasDomIdentity.PanelId(HostId, item.Id));
        builder.AddAttribute(
            6,
            "LabelledById",
            AtlasDomIdentity.ControlId(
                HostId,
                item.Id,
                Group.Kind == DockItemKind.Document ? "document-tab" : "tool-tab"));
        builder.AddAttribute(
            7,
            "RecoveryKey",
            (SandboxedFrameResolver, descriptor?.Revision ?? 0));
        builder.AddAttribute(8, "OnError", OnError);
        builder.AddAttribute(
            9,
            "SandboxedFrameResolver",
            SandboxedFrameResolver);
        builder.AddAttribute(10, "Generation", Generation);
        builder.AddComponentReferenceCapture(
            11,
            value =>
            {
                if (isSelected)
                {
                    selectedContentOutlet = (AtlasContentOutlet)value;
                }
            });
        builder.CloseComponent();
    }

    private void UpdateLifetimeCache()
    {
        if (ContentLifetimeOptions.Mode == AtlasContentLifetimeMode.Default
            || Group.SelectedItemId is null)
        {
            keptAliveItemIds.Clear();
            return;
        }

        var itemIds = Items.Select(item => item.Id).ToHashSet();
        keptAliveItemIds.RemoveAll(itemId => !itemIds.Contains(itemId));

        var selectedId = Group.SelectedItemId.Value;
        keptAliveItemIds.Remove(selectedId);
        keptAliveItemIds.Add(selectedId);
        while (keptAliveItemIds.Count > ContentLifetimeOptions.MaxCachedItems)
        {
            keptAliveItemIds.RemoveAt(0);
        }
    }

    private async Task SelectAsync(DockItemId itemId)
    {
        await Workspace.ExecuteAsync(new SelectItemOperation(itemId));
    }

    private DockItem SelectedItem()
    {
        var selectedId = Group.SelectedItemId
            ?? throw new InvalidOperationException("A non-empty group must select an item.");
        return Items.First(item => item.Id == selectedId);
    }

    private string GroupClass()
    {
        var kind = Group.Kind == DockItemKind.Tool ? "tool" : "document";
        var visibility = Group.Visibility == GroupVisibility.Expanded
            ? "expanded"
            : "collapsed";
        return $"atlas-v2-group atlas-v2-group-{kind} atlas-v2-group-{visibility}";
    }
}
