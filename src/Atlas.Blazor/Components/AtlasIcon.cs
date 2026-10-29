using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

/// <summary>
///     Atlas 内置图标名称。图标用于 Atlas 自有 chrome 控件；枚举值属于公开 API，
///     已有值不得删除或重排。
/// </summary>
public enum AtlasIconName
{
    /// <summary>关闭。</summary>
    Close,

    /// <summary>向上尖角。</summary>
    ChevronUp,

    /// <summary>向下尖角。</summary>
    ChevronDown,

    /// <summary>向左尖角。</summary>
    ChevronLeft,

    /// <summary>向右尖角。</summary>
    ChevronRight,
}

/// <summary>
///     渲染一个装饰性的 Atlas 图标。图标使用 <c>currentColor</c> 描边，
///     始终设置 <c>aria-hidden="true"</c> 与 <c>focusable="false"</c>；
///     可访问名称必须由承载图标的控件（例如带 <c>aria-label</c> 的按钮）提供。
/// </summary>
public sealed class AtlasIcon : ComponentBase
{
    private const string ClosePath = "m4 4 8 8M12 4 4 12";
    private const string ChevronUpPath = "m4 10 4-4 4 4";
    private const string ChevronDownPath = "m4 6 4 4 4-4";
    private const string ChevronLeftPath = "m10 12-4-4 4-4";
    private const string ChevronRightPath = "m6 4 4 4-4 4";

    /// <summary>
    ///     要渲染的图标。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public AtlasIconName Name { get; set; }

    /// <summary>
    ///     图标的像素边长；默认 16。
    /// </summary>
    [Parameter]
    public int Size { get; set; } = 16;

    /// <summary>
    ///     附加到图标根元素的 CSS 类；基础类 <c>atlas-v2-icon</c> 始终存在。
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(
            1,
            "class",
            string.IsNullOrWhiteSpace(CssClass)
                ? "atlas-v2-icon"
                : $"atlas-v2-icon {CssClass}");
        builder.AddAttribute(2, "width", Size);
        builder.AddAttribute(3, "height", Size);
        builder.AddAttribute(4, "viewBox", "0 0 16 16");
        builder.AddAttribute(5, "fill", "none");
        builder.AddAttribute(6, "stroke", "currentColor");
        builder.AddAttribute(7, "stroke-width", "1.5");
        builder.AddAttribute(8, "stroke-linecap", "round");
        builder.AddAttribute(9, "stroke-linejoin", "round");
        builder.AddAttribute(10, "aria-hidden", "true");
        builder.AddAttribute(11, "focusable", "false");
        builder.OpenElement(12, "path");
        builder.AddAttribute(13, "d", PathFor(Name));
        builder.CloseElement();
        builder.CloseElement();
    }

    private static string PathFor(AtlasIconName name)
    {
        return name switch
        {
            AtlasIconName.ChevronUp => ChevronUpPath,
            AtlasIconName.ChevronDown => ChevronDownPath,
            AtlasIconName.ChevronLeft => ChevronLeftPath,
            AtlasIconName.ChevronRight => ChevronRightPath,
            _ => ClosePath,
        };
    }
}
