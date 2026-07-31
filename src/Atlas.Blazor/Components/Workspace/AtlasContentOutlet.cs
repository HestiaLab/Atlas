using Atlas.Core;
using Atlas.Core.Items;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Atlas.Blazor.Components;

internal sealed class AtlasContentOutlet : ComponentBase
{
    private ElementReference element;

    [Parameter]
    public WorkspaceId WorkspaceId { get; set; }

    [Parameter]
    public DockItem Item { get; set; } = null!;

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public string PanelId { get; set; } = string.Empty;

    [Parameter]
    public string LabelledById { get; set; } = string.Empty;

    [Parameter]
    public object? RecoveryKey { get; set; }

    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    internal ValueTask FocusAsync()
    {
        return element.FocusAsync();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var sequence = 0;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "atlas-v2-content-outlet");
        builder.AddAttribute(sequence++, "id", PanelId);
        builder.AddAttribute(sequence++, "role", "tabpanel");
        builder.AddAttribute(sequence++, "aria-labelledby", LabelledById);
        builder.AddAttribute(sequence++, "data-atlas-item-id", Item.Id.Value);
        builder.AddAttribute(
            sequence++,
            "data-atlas-item-kind",
            Item.Kind.ToString().ToLowerInvariant());
        builder.AddAttribute(sequence++, "tabindex", "-1");
        builder.AddAttribute(sequence++, "hidden", !IsSelected);
        builder.AddAttribute(sequence++, "aria-hidden", IsSelected ? null : "true");
        builder.AddAttribute(sequence++, "inert", !IsSelected);
        builder.AddElementReferenceCapture(sequence++, value => element = value);

        builder.OpenComponent<AtlasContentErrorBoundary>(sequence++);
        builder.AddAttribute(
            sequence++,
            "SafeError",
            new AtlasHostError(
                AtlasHostErrorCode.ContentRenderFailed,
                "ATLAS-CONTENT-RENDER-FAILED",
                "Atlas.Content.RenderFailed",
                WorkspaceId,
                Item.Id));
        builder.AddAttribute(sequence++, "OnSafeError", OnError);
        builder.AddAttribute(sequence++, "RecoveryKey", (Item.Id, RecoveryKey));
        builder.AddAttribute(
            sequence++,
            "ErrorContent",
            (RenderFragment<Exception>)(_ => errorBuilder =>
            {
                errorBuilder.OpenElement(0, "div");
                errorBuilder.AddAttribute(1, "class", "atlas-v2-content-error");
                errorBuilder.AddContent(2, "Content could not be rendered.");
                errorBuilder.CloseElement();
            }));
        builder.AddAttribute(sequence++, "ChildContent", ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
    }
}
