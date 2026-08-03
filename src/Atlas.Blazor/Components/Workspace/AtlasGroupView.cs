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
    public RenderFragment<AtlasContentContext>? ContentTemplate { get; set; }

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

    protected override void OnParametersSet()
    {
        UpdateLifetimeCache();
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Group));
        foreach (var item in Items)
        {
            hash.Add(RuntimeHelpers.GetHashCode(item));
        }

        hash.Add(ContentTemplate);
        hash.Add(EmptyTemplate);
        hash.Add(HeaderTemplate);
        hash.Add(FocusCoordinator.Version);
        hash.Add(ContentLifetimeOptions.Mode);
        hash.Add(ContentLifetimeOptions.MaxCachedItems);
        hash.Add(SandboxedFrameResolver);
        hash.Add(Catalog);
        hash.Add(Generation);
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
        var sequence = 0;
        builder.OpenElement(sequence++, "section");
        builder.SetKey(Group.Id);
        builder.AddAttribute(sequence++, "class", GroupClass());
        builder.AddAttribute(sequence++, "data-atlas-node-id", Group.Id.Value);
        builder.AddAttribute(sequence++, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(sequence++, "data-atlas-group-kind", Group.Kind.ToString().ToLowerInvariant());
        builder.AddAttribute(sequence++, "role", "region");
        if (Group.SelectedItemId is not null)
        {
            var selected = Items.First(item => item.Id == Group.SelectedItemId.Value);
            builder.AddAttribute(
                sequence++,
                "aria-labelledby",
                Group.Kind == DockItemKind.Document
                    ? AtlasDomIdentity.ControlId(HostId, selected.Id, "document-tab")
                    : AtlasDomIdentity.HeadingId(HostId, selected.Id));
        }
        if (Group.LogicalRegion is not null)
        {
            builder.AddAttribute(
                sequence++,
                "data-atlas-logical-region",
                Group.LogicalRegion.Value.ToString());
        }

        if (Items.Count == 0)
        {
            RenderEmpty(builder, ref sequence);
        }
        else
        {
            if (Group.Kind == DockItemKind.Document)
            {
                builder.AddContent(
                    sequence++,
                    (RenderFragment)(headerBuilder =>
                    {
                        var headerSequence = 0;
                        RenderTabStrip(headerBuilder, ref headerSequence);
                    }));
            }
            else
            {
                builder.AddContent(
                    sequence++,
                    (RenderFragment)(headerBuilder =>
                    {
                        var headerSequence = 0;
                        RenderToolHeader(headerBuilder, ref headerSequence);
                    }));
            }

            builder.AddContent(
                sequence++,
                (RenderFragment)(contentBuilder =>
                {
                    var contentSequence = 0;
                    RenderContentOutlets(contentBuilder, ref contentSequence);
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
        if (ContentTemplate is not null || Group.SelectedItemId is null)
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

    private void RenderEmpty(RenderTreeBuilder builder, ref int sequence)
    {
        var context = new AtlasEmptyStateContext(
            Workspace.Id,
            Group.Id,
            Group.Kind,
            Group.RetentionPolicy);
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-empty-state");
        if (EmptyTemplate is not null)
        {
            builder.AddContent(sequence++, EmptyTemplate(context));
        }
        else
        {
            builder.AddContent(
                sequence++,
                Group.Kind == DockItemKind.Tool
                    ? "No tool content"
                    : "No document content");
        }

        builder.CloseElement();
    }

    private void RenderTabStrip(RenderTreeBuilder builder, ref int sequence)
    {
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-tab-strip");
        builder.AddAttribute(sequence++, "role", "tablist");
        builder.AddAttribute(sequence++, "aria-orientation", "horizontal");
        builder.AddAttribute(sequence++, "aria-label", $"{Group.Id.Value} documents");
        builder.AddAttribute(sequence++, "data-atlas-group-id", Group.Id.Value);
        for (var itemIndex = 0; itemIndex < Items.Count; itemIndex++)
        {
            var item = Items[itemIndex];
            var selected = item.Id == Group.SelectedItemId;
            builder.OpenElement(sequence++, "button");
            builder.SetKey(item.Id);
            builder.AddAttribute(sequence++, "type", "button");
            builder.AddAttribute(sequence++, "role", "tab");
            builder.AddAttribute(
                sequence++,
                "id",
                AtlasDomIdentity.ControlId(HostId, item.Id, "document-tab"));
            builder.AddAttribute(sequence++, "aria-label", item.Title);
            builder.AddAttribute(
                sequence++,
                "aria-controls",
                AtlasDomIdentity.PanelId(HostId, item.Id));
            builder.AddAttribute(sequence++, "data-atlas-item-id", item.Id.Value);
            builder.AddAttribute(sequence++, "data-atlas-item-kind", "document");
            builder.AddAttribute(sequence++, "data-atlas-drag-kind", "document-tab");
            builder.AddAttribute(sequence++, "data-atlas-group-id", Group.Id.Value);
            builder.AddAttribute(
                sequence++,
                "data-atlas-insertion-index",
                itemIndex.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                sequence++,
                "data-atlas-command-capabilities",
                ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(sequence++, "aria-selected", selected);
            builder.AddAttribute(sequence++, "tabindex", selected ? "0" : "-1");
            builder.AddAttribute(
                sequence++,
                "onclick",
                EventCallback.Factory.Create(this, () => SelectAsync(item.Id)));
            RenderHeader(builder, ref sequence, item, selected);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderToolHeader(RenderTreeBuilder builder, ref int sequence)
    {
        var selected = SelectedItem();
        builder.OpenElement(sequence++, "header");
        builder.AddAttribute(sequence++, "class", "atlas-v2-panel-header");
        builder.AddAttribute(sequence++, "role", "heading");
        builder.AddAttribute(sequence++, "aria-level", "2");
        builder.AddAttribute(
            sequence++,
            "id",
            AtlasDomIdentity.HeadingId(HostId, selected.Id));
        builder.AddAttribute(sequence++, "data-atlas-panel-header", "true");
        builder.AddAttribute(sequence++, "data-atlas-drag-kind", "panel-header");
        builder.AddAttribute(sequence++, "data-atlas-item-id", selected.Id.Value);
        builder.AddAttribute(sequence++, "data-atlas-item-kind", "tool");
        builder.AddAttribute(sequence++, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(
            sequence++,
            "data-atlas-command-capabilities",
            ((int)selected.Capabilities).ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(
            sequence++,
            "data-atlas-insertion-index",
            Group.ItemIds.ToList().IndexOf(selected.Id).ToString(CultureInfo.InvariantCulture));
        RenderHeader(builder, ref sequence, selected, isSelected: true);
        builder.CloseElement();
    }

    private void RenderHeader(
        RenderTreeBuilder builder,
        ref int sequence,
        DockItem item,
        bool isSelected)
    {
        if (HeaderTemplate is not null)
        {
            builder.AddContent(
                sequence++,
                HeaderTemplate(new AtlasItemHeaderContext(item, isSelected)));
            return;
        }

        if (item.IconKey is not null)
        {
            builder.OpenElement(sequence++, "span");
            builder.AddAttribute(sequence++, "class", "atlas-v2-item-icon");
            builder.AddAttribute(sequence++, "data-atlas-icon", item.IconKey);
            builder.CloseElement();
        }

        builder.OpenElement(sequence++, "span");
        builder.AddAttribute(sequence++, "class", "atlas-v2-item-title");
        builder.AddAttribute(sequence++, "title", item.Title);
        builder.AddContent(sequence++, item.Title);
        builder.CloseElement();
    }

    private void RenderContentOutlets(RenderTreeBuilder builder, ref int sequence)
    {
        var selectedId = Group.SelectedItemId
            ?? throw new InvalidOperationException("A non-empty group must select an item.");
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-content-stack");
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
        var sequence = 0;
        var context = new AtlasContentContext(Workspace, item);
        var resolution = Catalog.TryResolve(item.Content.Kind, out var descriptor);
        builder.OpenComponent<AtlasContentOutlet>(sequence++);
        builder.SetKey((item.Id, descriptor?.Revision ?? 0));
        builder.AddAttribute(sequence++, "Content", context);
        builder.AddAttribute(sequence++, "Resolution", resolution);
        builder.AddAttribute(sequence++, "Descriptor", descriptor);
        builder.AddAttribute(sequence++, "IsSelected", isSelected);
        builder.AddAttribute(sequence++, "PanelId", AtlasDomIdentity.PanelId(HostId, item.Id));
        builder.AddAttribute(
            sequence++,
            "LabelledById",
            AtlasDomIdentity.ControlId(
                HostId,
                item.Id,
                Group.Kind == DockItemKind.Document ? "document-tab" : "tool-tab"));
        builder.AddAttribute(
            sequence++,
            "RecoveryKey",
            (ContentTemplate, SandboxedFrameResolver, descriptor?.Revision ?? 0));
        builder.AddAttribute(sequence++, "OnError", OnError);
        builder.AddAttribute(
            sequence++,
            "SandboxedFrameResolver",
            SandboxedFrameResolver);
        builder.AddAttribute(sequence++, "ContentTemplate", ContentTemplate);
        builder.AddAttribute(sequence++, "Generation", Generation);
        builder.AddComponentReferenceCapture(
            sequence++,
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
