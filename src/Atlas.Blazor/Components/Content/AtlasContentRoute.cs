using Atlas.Blazor.Content;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

/// <summary>
///     一条已收集的宿主本地路由声明。
/// </summary>
internal sealed record AtlasContentRouteDescriptor(
    string Kind,
    RenderFragment<AtlasContentContext>? ChildContent);

/// <summary>
///     收集在宿主的内部路由收集器下渲染的 <see cref="AtlasContentRoute"/> 声明。
///     出错的声明会使整轮收集失效，
///     宿主保留先前已提交的 Route/Map 生成。
/// </summary>
internal sealed class AtlasContentRouteCollector
{
    // 已收集的路由声明；构建时按收集顺序提交。
    private readonly List<AtlasContentRouteDescriptor> routes = new();

    // 无效声明的锁存失败位；置位后整轮收集失效。
    private bool invalid;

    /// <summary>
    ///     登记一条路由声明；空白种类或缺失模板会使整轮收集失效。
    /// </summary>
    /// <param name="kind">路由拥有的内容种类。</param>
    /// <param name="childContent">为匹配的种类渲染的内容模板。</param>
    internal void AddRoute(string kind, RenderFragment<AtlasContentContext>? childContent)
    {
        if (string.IsNullOrWhiteSpace(kind) || childContent is null)
        {
            invalid = true;
            return;
        }

        routes.Add(new AtlasContentRouteDescriptor(kind, childContent));
    }

    /// <summary>
    ///     将收集器标记为无效；后续构建失败，宿主保留先前已提交的 Route/Map 生成。
    /// </summary>
    internal void Invalidate()
    {
        invalid = true;
    }

    /// <summary>
    ///     取回已收集的路由声明；收集无效时返回失败且不输出路由。
    /// </summary>
    /// <param name="collected">成功时输出的路由列表；失败时为空列表。</param>
    /// <returns>
    ///     收集有效返回 <see langword="true"/>；出现过无效声明时返回 <see langword="false"/>。
    /// </returns>
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
///     声明宿主本地内容路由。
///     路由声明是无渲染的注册组件；
///     其 <see cref="ChildContent"/> 仅在宿主解析出匹配的 <c>ContentReference.Kind</c> 后才渲染。
///     空白的 <see cref="Kind"/> 或为 null 的 <see cref="ChildContent"/>
///     会报告 <see cref="AtlasHostErrorCode.DefinitionInvalid"/>，且不贡献渲染器。
/// </summary>
public sealed class AtlasContentRoute : ComponentBase
{
    /// <summary>
    ///     此路由拥有的内容种类；空白值使该声明无效。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Kind { get; set; } = null!;

    /// <summary>
    ///     为匹配的 Kind 渲染的内容模板；为 null 时该声明无效。
    /// </summary>
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
        // 路由是无渲染的注册组件；它们不产生任何 DOM。
    }
}
