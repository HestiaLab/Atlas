using Atlas.Blazor.Content;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

/// <summary>One collected Host-local route declaration.</summary>
internal sealed record AtlasContentRouteDescriptor(
    string Kind,
    RenderFragment<AtlasContentContext>? ChildContent);

/// <summary>
/// Collects <see cref="AtlasContentRoute"/> declarations rendered under the Host's
/// internal route collector. A faulting declaration invalidates the whole pass so
/// the Host keeps the previously committed Route/Map generation.
/// </summary>
internal sealed class AtlasContentRouteCollector
{
    private readonly List<AtlasContentRouteDescriptor> routes = new();
    private bool invalid;

    internal void AddRoute(string kind, RenderFragment<AtlasContentContext>? childContent)
    {
        if (string.IsNullOrWhiteSpace(kind) || childContent is null)
        {
            invalid = true;
            return;
        }

        routes.Add(new AtlasContentRouteDescriptor(kind, childContent));
    }

    internal void Invalidate()
    {
        invalid = true;
    }

    internal bool TryBuild(out IReadOnlyList<AtlasContentRouteDescriptor> collected)
    {
        if (invalid)
        {
            collected = Array.Empty<AtlasContentRouteDescriptor>();
            return false;
        }

        collected = routes;
        return true;
    }
}

/// <summary>
/// Declares a Host-local content route. Route declarations are renderless
/// registration components; their <see cref="ChildContent"/> renders only after a
/// matching <c>ContentReference.Kind</c> is resolved by the Host. A blank
/// <see cref="Kind"/> or null <see cref="ChildContent"/> reports
/// <see cref="AtlasHostErrorCode.DefinitionInvalid"/> and contributes no renderer.
/// </summary>
public sealed class AtlasContentRoute : ComponentBase
{
    /// <summary>Gets or sets the ordinal content kind this route owns.</summary>
    [Parameter]
    [EditorRequired]
    public string Kind { get; set; } = null!;

    /// <summary>Gets or sets the content template rendered for the matched Kind.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment<AtlasContentContext>? ChildContent { get; set; }

    [CascadingParameter]
    private AtlasContentRouteCollector Collector { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Collector.AddRoute(Kind, ChildContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Routes are renderless registration components; they emit no DOM.
    }
}
