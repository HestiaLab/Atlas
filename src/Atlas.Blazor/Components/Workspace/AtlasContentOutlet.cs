using Atlas.Blazor.Content;
using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
/// Owns one Item's guarded content projection: evaluates the Trust/Sandbox
/// resolver exactly once inside the ErrorBoundary boundary, fails closed when the
/// resolver throws, and otherwise renders the effective Catalog resolution.
/// </summary>
internal sealed class AtlasContentOutlet : ComponentBase
{
    private ElementReference element;

    [Parameter]
    public AtlasContentContext Content { get; set; } = null!;

    [Parameter]
    public AtlasContentResolution Resolution { get; set; }

    [Parameter]
    public AtlasContentRegistrationDescriptor? Descriptor { get; set; }

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
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    [Parameter]
    public RenderFragment<AtlasContentContext>? ContentTemplate { get; set; }

    [Parameter]
    public long Generation { get; set; }

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
        builder.AddAttribute(sequence++, "data-atlas-item-id", Content.ItemId.Value);
        builder.AddAttribute(
            sequence++,
            "data-atlas-item-kind",
            Content.Item.Kind.ToString().ToLowerInvariant());
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
                Content.WorkspaceId,
                Content.ItemId));
        builder.AddAttribute(sequence++, "OnSafeError", OnError);
        builder.AddAttribute(sequence++, "RecoveryKey", (Content.ItemId, RecoveryKey));
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
        builder.AddAttribute(
            sequence++,
            "ChildContent",
            (RenderFragment)(contentBuilder => RenderProjectedContent(contentBuilder)));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private void RenderProjectedContent(RenderTreeBuilder builder)
    {
        // 1. Trust policy precedes Catalog resolution. The Sandbox resolver is
        //    evaluated exactly once inside this guarded fragment; an exception
        //    fails closed here and never reaches the Catalog or Binding factories.
        var sandbox = SandboxedFrameResolver?.Invoke(Content);
        if (sandbox is not null)
        {
            RenderSandboxedFrame(builder, sandbox);
            return;
        }

        // 2. Temporary compatibility path. The legacy ContentTemplate branch is
        //    retained only until Phase 4 migrates all repository consumers; it is
        //    not part of the final architecture.
        // TODO(phase-4): remove ContentTemplate after the Demo and tests migrate.
        if (ContentTemplate is not null)
        {
            builder.AddContent(0, ContentTemplate(Content));
            return;
        }

        switch (Resolution)
        {
            case AtlasContentResolution.Registered:
                RenderRegistered(builder, Descriptor!);
                break;
            case AtlasContentResolution.Conflict:
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "atlas-v2-content-conflict");
                builder.AddAttribute(2, "data-atlas-content-kind", Content.Reference.Kind);
                builder.AddContent(3, "Content configuration conflict.");
                builder.CloseElement();
                break;
            default:
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "atlas-v2-content-missing");
                builder.AddAttribute(2, "data-atlas-content-kind", Content.Reference.Kind);
                builder.AddContent(3, "Content is not registered.");
                builder.CloseElement();
                break;
        }
    }

    private void RenderRegistered(
        RenderTreeBuilder builder,
        AtlasContentRegistrationDescriptor descriptor)
    {
        switch (descriptor.Source)
        {
            case AtlasContentRegistrationSource.DI:
                builder.OpenComponent<DynamicComponent>(0);
                builder.AddAttribute(1, "Type", descriptor.ComponentType!);
                builder.AddAttribute(
                    2,
                    "Parameters",
                    new Dictionary<string, object?>
                    {
                        [nameof(AtlasContentComponent.Content)] = Content,
                    });
                builder.CloseComponent();
                break;
            default:
                // Route (and Map in Phase 3) descriptors carry a Host-local
                // renderer recipe; the owning Blazor Renderer applies its
                // parameters through the normal SetParametersAsync lifecycle.
                builder.AddContent(0, descriptor.Renderer!(Content));
                break;
        }
    }

    private void RenderSandboxedFrame(RenderTreeBuilder builder, AtlasSandboxedFrameOptions options)
    {
        builder.OpenElement(0, "iframe");
        builder.AddAttribute(1, "class", "atlas-v2-sandbox-frame");
        builder.AddAttribute(2, "data-atlas-sandbox-frame", "true");
        builder.AddAttribute(3, "data-atlas-protocol-version", "1");
        builder.AddAttribute(4, "data-atlas-workspace-id", Content.WorkspaceId.Value);
        builder.AddAttribute(5, "data-atlas-item-id", Content.ItemId.Value);
        builder.AddAttribute(
            6,
            "data-atlas-generation",
            Generation.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(
            7,
            "data-atlas-max-message-bytes",
            options.MaxMessageBytes.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(8, "sandbox", "allow-scripts");
        builder.AddAttribute(9, "referrerpolicy", "no-referrer");
        builder.AddAttribute(10, "loading", "lazy");
        builder.AddAttribute(11, "title", options.Title);
        builder.AddAttribute(12, "src", options.Source);
        builder.CloseElement();
    }
}
