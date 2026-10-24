using Atlas.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     标签页溢出菜单与右键菜单共用的弹出菜单：负责视口内定位、
///     首项聚焦、方向键漫游与 Escape/Tab 关闭。
/// </summary>
internal sealed class AtlasPopupMenu : ComponentBase
{
    private readonly List<ElementReference> itemElements = new();
    private int focusedIndex = -1;
    private ElementReference menuElement;

    /// <summary>
    ///     菜单的无障碍名称。
    /// </summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    ///     菜单 DOM id；由触发按钮的 aria-controls 引用。
    /// </summary>
    [Parameter]
    public string MenuId { get; set; } = string.Empty;

    /// <summary>
    ///     区分溢出菜单与右键菜单的顶层标记属性名。
    /// </summary>
    [Parameter]
    public string MarkerAttribute { get; set; } = string.Empty;

    /// <summary>
    ///     指针点击位置（视口坐标）。
    /// </summary>
    [Parameter]
    public double X { get; set; }

    /// <summary>
    ///     指针点击位置（视口坐标）。
    /// </summary>
    [Parameter]
    public double Y { get; set; }

    /// <summary>
    ///     键盘打开时没有指针坐标，使用视口内固定回退位置。
    /// </summary>
    [Parameter]
    public bool UseFallbackPosition { get; set; }

    /// <summary>
    ///     菜单项；禁用项不可聚焦且不执行。
    /// </summary>
    [Parameter]
    public IReadOnlyList<AtlasPopupMenuItem> Items { get; set; } =
        Array.Empty<AtlasPopupMenuItem>();

    /// <summary>
    ///     请求关闭菜单；由父组件移除本组件。
    /// </summary>
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-popup-backdrop");
        builder.AddAttribute(2, "data-atlas-popup-backdrop", "true");
        builder.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, DismissAsync));
        builder.AddAttribute(
            4,
            "oncontextmenu",
            EventCallback.Factory.Create<MouseEventArgs>(this, _ => DismissAsync()));
        builder.AddAttribute(5, "oncontextmenu:preventDefault", true);
        builder.CloseElement();

        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "atlas-v2-popup");
        builder.AddAttribute(8, "id", MenuId);
        builder.AddAttribute(9, "role", "menu");
        builder.AddAttribute(10, "aria-label", Label);
        builder.AddAttribute(11, "tabindex", "-1");
        builder.AddAttribute(12, "style", MenuStyle());
        builder.AddAttribute(13, MarkerAttribute, "true");
        builder.AddAttribute(
            14,
            "onkeydown",
            EventCallback.Factory.Create<KeyboardEventArgs>(this, OnKeyDownAsync));
        builder.AddAttribute(15, "onkeydown:stopPropagation", true);
        builder.AddElementReferenceCapture(16, value => menuElement = value);
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            builder.OpenElement(0, "button");
            builder.SetKey(item.Command);
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "role", "menuitem");
            builder.AddAttribute(3, "class", "atlas-v2-popup-item");
            builder.AddAttribute(4, "data-atlas-menu-command", item.Command);
            if (item.IsTabListItem)
            {
                builder.AddAttribute(5, "data-atlas-tab-menu-item", "true");
                builder.AddAttribute(6, "data-atlas-item-id", item.ItemId?.Value);
            }

            if (item.Disabled)
            {
                builder.AddAttribute(7, "disabled", true);
            }

            builder.AddAttribute(
                8,
                "onclick",
                EventCallback.Factory.Create(this, () => InvokeAsync(item)));
            var itemIndex = index;
            builder.AddElementReferenceCapture(
                9,
                value =>
                {
                    while (itemElements.Count <= itemIndex)
                    {
                        itemElements.Add(default);
                    }

                    itemElements[itemIndex] = value;
                });
            builder.AddContent(10, item.Label);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var firstEnabled = FirstEnabledIndex();
        if (firstEnabled >= 0)
        {
            await FocusItemAsync(firstEnabled);
            return;
        }

        // 全部命令禁用时没有可聚焦菜单项；聚焦菜单容器，
        // 保证 Escape/Tab 仍能关闭菜单（REQ-F-138 AC2）。
        try
        {
            await menuElement.FocusAsync();
        }
        catch (JSException)
        {
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                await DismissAsync();
                return;
            case "Tab":
                // 菜单不是模态对话框；Tab 离开时关闭并把焦点还给触发点。
                await DismissAsync();
                return;
            case "ArrowDown":
                await FocusRelativeAsync(1);
                return;
            case "ArrowUp":
                await FocusRelativeAsync(-1);
                return;
            case "Home":
                await FocusItemAsync(FirstEnabledIndex());
                return;
            case "End":
                await FocusItemAsync(LastEnabledIndex());
                return;
        }
    }

    private async Task FocusRelativeAsync(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var start = focusedIndex >= 0
            ? focusedIndex
            : direction > 0
                ? -1
                : Items.Count;
        for (var step = 1; step <= Items.Count; step++)
        {
            var index = (start + (direction * step) + Items.Count * 2) % Items.Count;
            if (!Items[index].Disabled)
            {
                await FocusItemAsync(index);
                return;
            }
        }
    }

    private int FirstEnabledIndex()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (!Items[index].Disabled)
            {
                return index;
            }
        }

        return -1;
    }

    private int LastEnabledIndex()
    {
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!Items[index].Disabled)
            {
                return index;
            }
        }

        return -1;
    }

    private async Task FocusItemAsync(int index)
    {
        if (index < 0 || index >= itemElements.Count || Items[index].Disabled)
        {
            return;
        }

        try
        {
            await itemElements[index].FocusAsync();
            focusedIndex = index;
        }
        catch (JSException)
        {
            // 浏览器线路已释放或元素已被重渲染替换；菜单仍可关闭。
        }
    }

    private async Task InvokeAsync(AtlasPopupMenuItem item)
    {
        if (item.Disabled)
        {
            return;
        }

        // 先关闭菜单再执行命令：命令可能移除目标标签页，
        // 菜单组件不应继续持有已移除项的元素引用。
        await DismissAsync();
        await item.Invoke();
    }

    private Task DismissAsync()
    {
        return OnDismiss.InvokeAsync();
    }

    private string MenuStyle()
    {
        if (UseFallbackPosition)
        {
            return "inset-inline-end: 0.5rem; inset-block-start: var(--atlas-chrome-height);";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: min({X:0.###}px, max(0px, calc(100vw - var(--atlas-popup-inline-size)))); "
            + $"top: min({Y:0.###}px, max(0px, calc(100vh - var(--atlas-popup-max-block-size))));");
    }
}

/// <summary>
///     弹出菜单中的单项命令。
/// </summary>
internal sealed record AtlasPopupMenuItem(
    string Command,
    string Label,
    bool Disabled,
    Func<Task> Invoke,
    DockItemId? ItemId = null,
    bool IsTabListItem = false);
