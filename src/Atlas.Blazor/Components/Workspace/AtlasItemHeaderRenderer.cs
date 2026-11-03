using Atlas.Blazor.Content;
using Atlas.Core.Items;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Atlas.Blazor.Components;

/// <summary>
///     工具面板标题内容的共享投影：Dock 组与浮动窗口使用同一套标题、
///     图标与自定义 HeaderTemplate 语义。
/// </summary>
internal static class AtlasItemHeaderRenderer
{
    internal static void Render(
        RenderTreeBuilder builder,
        DockItem item,
        bool isSelected,
        RenderFragment<AtlasItemHeaderContext>? headerTemplate)
    {
        if (headerTemplate is not null)
        {
            builder.AddContent(
                0,
                headerTemplate(new AtlasItemHeaderContext(item, isSelected)));
            return;
        }

        // 标题 span 固定占用序列 (3..6)，无论图标分支是否渲染，
        // 这样切换 IconKey 不会让标题在 diff 树中重新编号。
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
}
