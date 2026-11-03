using System.Globalization;
using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Placement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Atlas.Blazor.Components;

/// <summary>
///     单个浮动窗口的投影：复用工具面板标题与内容出口，附加窗口语义、
///     几何样式与八向缩放命中区。
/// </summary>
internal sealed class AtlasFloatingWindow : ComponentBase
{
    private ElementReference root;
    private AtlasContentOutlet? contentOutlet;

    [Parameter]
    public IAtlasWorkspace Workspace { get; set; } = null!;

    [Parameter]
    public WorkspaceSnapshot Snapshot { get; set; } = null!;

    [Parameter]
    public DockItem Item { get; set; } = null!;

    [Parameter]
    public FloatingWindowState State { get; set; } = null!;

    [Parameter]
    public AtlasContentCatalog Catalog { get; set; } = AtlasContentCatalog.Empty;

    [Parameter]
    public IAtlasContentStateScope? StateScope { get; set; }

    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? HeaderTemplate { get; set; }

    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    [Parameter]
    public long Generation { get; set; }

    [Parameter]
    public string HostId { get; set; } = string.Empty;

    [Parameter]
    public AtlasFocusCoordinator FocusCoordinator { get; set; } = null!;

    [Parameter]
    public int ZIndex { get; set; }

    [Parameter]
    public EventCallback<DockItemId> OnActivate { get; set; }

    [Parameter]
    public EventCallback<DockItemId> OnDock { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var headingId = AtlasDomIdentity.HeadingId(HostId, Item.Id);
        builder.OpenElement(0, "div");
        builder.SetKey(Item.Id);
        builder.AddAttribute(1, "class", "atlas-v2-floating-window");
        builder.AddAttribute(2, "role", "dialog");
        builder.AddAttribute(3, "aria-modal", "false");
        builder.AddAttribute(4, "aria-label", Item.Title);
        builder.AddAttribute(5, "tabindex", "-1");
        builder.AddAttribute(6, "data-atlas-floating-window", "true");
        builder.AddAttribute(7, "data-atlas-host-id", HostId);
        builder.AddAttribute(8, "data-atlas-item-id", Item.Id.Value);
        builder.AddAttribute(9, "data-atlas-item-kind", "tool");
        builder.AddAttribute(
            10,
            "data-atlas-revision",
            Snapshot.Revision.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(11, "style", GeometryStyle());
        builder.AddAttribute(
            12,
            "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, ActivateAsync));
        builder.AddElementReferenceCapture(13, value => root = value);

        RenderHeader(builder, headingId);
        RenderContent(builder, headingId);
        RenderResizeHandles(builder);
        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _ = firstRender;
        if (!FocusCoordinator.TryClaim(Item.Id, Snapshot.Revision))
        {
            return;
        }

        try
        {
            if (contentOutlet is not null)
            {
                await contentOutlet.FocusAsync();
            }
            else
            {
                await root.FocusAsync();
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
                        Item.Id));
            }
        }
    }

    private void RenderHeader(RenderTreeBuilder builder, string headingId)
    {
        builder.OpenElement(0, "header");
        builder.AddAttribute(1, "class", "atlas-v2-panel-header atlas-v2-floating-header");
        builder.AddAttribute(2, "role", "heading");
        builder.AddAttribute(3, "aria-level", "2");
        builder.AddAttribute(4, "id", headingId);
        builder.AddAttribute(5, "data-atlas-panel-header", "true");
        builder.AddAttribute(6, "data-atlas-drag-kind", "floating-window");
        builder.AddAttribute(7, "data-atlas-item-id", Item.Id.Value);
        builder.AddAttribute(8, "data-atlas-item-kind", "tool");
        builder.AddAttribute(9, "data-atlas-floating-item-id", Item.Id.Value);
        builder.AddAttribute(
            10,
            "data-atlas-command-capabilities",
            ((int)Item.Capabilities).ToString(CultureInfo.InvariantCulture));
        AtlasItemHeaderRenderer.Render(builder, Item, isSelected: true, HeaderTemplate);
        builder.OpenElement(11, "button");
        builder.SetKey((Item.Id, "floating-dock"));
        builder.AddAttribute(12, "type", "button");
        builder.AddAttribute(13, "class", "atlas-v2-panel-close");
        builder.AddAttribute(14, "data-atlas-close-floating", "true");
        builder.AddAttribute(15, "aria-label", $"Close floating window {Item.Title}");
        builder.AddAttribute(
            16,
            "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, DockAsync));
        builder.OpenComponent<AtlasIcon>(17);
        builder.AddAttribute(18, "Name", AtlasIconName.Close);
        builder.AddAttribute(19, "Size", 14);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
    }

    private void RenderContent(RenderTreeBuilder builder, string headingId)
    {
        var context = new AtlasContentContext(Workspace, Item, StateScope);
        var resolution = Catalog.TryResolve(Item.Content.Kind, out var descriptor);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-floating-content");
        builder.OpenComponent<AtlasContentOutlet>(2);
        builder.SetKey((Item.Id, descriptor?.Revision ?? 0));
        builder.AddAttribute(3, "Content", context);
        builder.AddAttribute(4, "Resolution", resolution);
        builder.AddAttribute(5, "Descriptor", descriptor);
        builder.AddAttribute(6, "IsSelected", true);
        builder.AddAttribute(7, "PanelId", AtlasDomIdentity.PanelId(HostId, Item.Id));
        builder.AddAttribute(8, "PanelRole", "region");
        builder.AddAttribute(9, "LabelledById", headingId);
        builder.AddAttribute(
            10,
            "RecoveryKey",
            (SandboxedFrameResolver, descriptor?.Revision ?? 0));
        builder.AddAttribute(11, "OnError", OnError);
        builder.AddAttribute(12, "SandboxedFrameResolver", SandboxedFrameResolver);
        builder.AddAttribute(13, "Generation", Generation);
        builder.AddComponentReferenceCapture(
            14,
            value => contentOutlet = (AtlasContentOutlet)value);
        builder.CloseComponent();
        builder.CloseElement();
    }

    private static void RenderResizeHandles(RenderTreeBuilder builder)
    {
        foreach (var direction in ResizeDirections)
        {
            builder.OpenElement(0, "div");
            builder.SetKey(direction);
            builder.AddAttribute(
                1,
                "class",
                $"atlas-v2-floating-resize atlas-v2-floating-resize-{direction}");
            builder.AddAttribute(2, "data-atlas-floating-resize", direction);
            builder.AddAttribute(3, "aria-hidden", "true");
            builder.CloseElement();
        }
    }

    private string GeometryStyle()
    {
        var geometry = State.Geometry;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:max(0px,min({geometry.X}px,calc(100% - {geometry.Width}px)));"
            + $"top:max(0px,min({geometry.Y}px,calc(100% - {geometry.Height}px)));"
            + $"width:min({geometry.Width}px,100%);height:min({geometry.Height}px,100%);"
            + $"z-index:{ZIndex};");
    }

    private async Task ActivateAsync(MouseEventArgs args)
    {
        _ = args;
        if (OnActivate.HasDelegate)
        {
            await OnActivate.InvokeAsync(Item.Id);
        }
    }

    private async Task DockAsync(MouseEventArgs args)
    {
        _ = args;
        if (OnDock.HasDelegate)
        {
            await OnDock.InvokeAsync(Item.Id);
        }
    }

    private static readonly string[] ResizeDirections =
    [
        "n",
        "s",
        "e",
        "w",
        "ne",
        "nw",
        "se",
        "sw",
    ];
}
