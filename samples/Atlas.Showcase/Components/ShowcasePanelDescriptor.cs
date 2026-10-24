using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;

namespace Atlas.Showcase.Components;

/// <summary>
///     Showcase 中所有内容 Kind 的唯一常量源，供布局定义与内容路由共用。
/// </summary>
public static class ShowcaseContentKinds
{
    public const string Catalog = "showcase-catalog";
    public const string Layout = "showcase-layout";
    public const string Content = "showcase-content";
    public const string DragDrop = "showcase-dragdrop";
    public const string Splitter = "showcase-splitter";
    public const string Keyboard = "showcase-keyboard";
    public const string Theme = "showcase-theme";
    public const string Persistence = "showcase-persistence";
    public const string Guide = "showcase-guide";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Catalog,
        Layout,
        Content,
        DragDrop,
        Splitter,
        Keyboard,
        Theme,
        Persistence,
        Guide,
    };
}

/// <summary>
///     描述一个示例工具面板；布局、教学目录与快捷面板共用这份元数据。
/// </summary>
/// <param name="Key">稳定的菜单/目录键。</param>
/// <param name="Title">面向用户的中文标题。</param>
/// <param name="Index">教学目录中的两位序号。</param>
/// <param name="ContentKind">内容目录中的 Kind。</param>
/// <param name="ItemId">工作区条目标识。</param>
/// <param name="DefaultGroupId">标准布局中的默认 Group。</param>
/// <param name="Region">默认逻辑区域。</param>
/// <param name="GroupOrder">同一 Group 内的顺序。</param>
public sealed record ShowcasePanelDescriptor(
    string Key,
    string Title,
    string Index,
    string ContentKind,
    DockItemId ItemId,
    LayoutNodeId DefaultGroupId,
    LogicalRegion Region,
    int GroupOrder);

/// <summary>
///     描述一个可移动目标 Group。
/// </summary>
public sealed record ShowcaseGroupDescriptor(
    LayoutNodeId Id,
    LogicalRegion Region,
    string Label);

/// <summary>
///     面板在当前工作区快照中的视图状态。
/// </summary>
/// <param name="Panel">面板元数据。</param>
/// <param name="GroupId">当前所在 Group；不存在时为默认值。</param>
/// <param name="Visibility">当前 Group 可见性。</param>
/// <param name="IsSelected">是否为所在工具 Group 的选中项。</param>
/// <param name="Exists">条目当前是否存在于工作区。</param>
public sealed record ShowcasePanelView(
    ShowcasePanelDescriptor Panel,
    LayoutNodeId GroupId,
    GroupVisibility Visibility,
    bool IsSelected,
    bool Exists)
{
    public bool GroupVisible => Exists && Visibility == GroupVisibility.Expanded;
}

/// <summary>
///     描述一个示例文档标签页。
/// </summary>
public sealed record ShowcaseDocumentDescriptor(
    string Key,
    string Title,
    string ContentKind,
    DockItemId ItemId,
    string ResourceId,
    bool IsPinned);

/// <summary>
///     示例工作区的单一元数据源。
/// </summary>
public static class ShowcasePanels
{
    public static readonly ShowcaseGroupDescriptor InlineStartUpper = new(
        new LayoutNodeId("left-upper"),
        LogicalRegion.InlineStartUpper,
        "左上");

    public static readonly ShowcaseGroupDescriptor InlineStartLower = new(
        new LayoutNodeId("left-lower"),
        LogicalRegion.InlineStartLower,
        "左下");

    public static readonly ShowcaseGroupDescriptor InlineEndUpper = new(
        new LayoutNodeId("right-upper"),
        LogicalRegion.InlineEndUpper,
        "右上");

    public static readonly ShowcaseGroupDescriptor InlineEndLower = new(
        new LayoutNodeId("right-lower"),
        LogicalRegion.InlineEndLower,
        "右下");

    public static readonly ShowcaseGroupDescriptor BlockEndInlineStart = new(
        new LayoutNodeId("bottom-left"),
        LogicalRegion.BlockEndInlineStart,
        "底部左");

    public static readonly ShowcaseGroupDescriptor BlockEndInlineEnd = new(
        new LayoutNodeId("bottom-right"),
        LogicalRegion.BlockEndInlineEnd,
        "底部右");

    public static readonly IReadOnlyList<ShowcaseGroupDescriptor> Groups =
        new[]
        {
            InlineStartUpper,
            InlineStartLower,
            InlineEndUpper,
            InlineEndLower,
            BlockEndInlineStart,
            BlockEndInlineEnd,
        };

    public static readonly ShowcasePanelDescriptor Catalog = new(
        "catalog",
        "教学目录",
        "01",
        ShowcaseContentKinds.Catalog,
        new DockItemId("showcase-catalog"),
        InlineStartUpper.Id,
        InlineStartUpper.Region,
        0);

    public static readonly ShowcasePanelDescriptor Layout = new(
        "layout",
        "声明式布局",
        "02",
        ShowcaseContentKinds.Layout,
        new DockItemId("showcase-layout"),
        InlineStartLower.Id,
        InlineStartLower.Region,
        0);

    public static readonly ShowcasePanelDescriptor Content = new(
        "content",
        "内容注册",
        "03",
        ShowcaseContentKinds.Content,
        new DockItemId("showcase-content"),
        InlineEndUpper.Id,
        InlineEndUpper.Region,
        0);

    public static readonly ShowcasePanelDescriptor DragDrop = new(
        "dragdrop",
        "拖拽与停靠",
        "04",
        ShowcaseContentKinds.DragDrop,
        new DockItemId("showcase-dragdrop"),
        InlineEndUpper.Id,
        InlineEndUpper.Region,
        1);

    public static readonly ShowcasePanelDescriptor Splitter = new(
        "splitter",
        "分割条",
        "05",
        ShowcaseContentKinds.Splitter,
        new DockItemId("showcase-splitter"),
        BlockEndInlineStart.Id,
        BlockEndInlineStart.Region,
        1);

    public static readonly ShowcasePanelDescriptor Keyboard = new(
        "keyboard",
        "键盘操作",
        "06",
        ShowcaseContentKinds.Keyboard,
        new DockItemId("showcase-keyboard"),
        InlineEndLower.Id,
        InlineEndLower.Region,
        0);

    public static readonly ShowcasePanelDescriptor Theme = new(
        "theme",
        "主题定制",
        "07",
        ShowcaseContentKinds.Theme,
        new DockItemId("showcase-theme"),
        BlockEndInlineStart.Id,
        BlockEndInlineStart.Region,
        0);

    public static readonly ShowcasePanelDescriptor Persistence = new(
        "persistence",
        "持久化",
        "08",
        ShowcaseContentKinds.Persistence,
        new DockItemId("showcase-persistence"),
        BlockEndInlineEnd.Id,
        BlockEndInlineEnd.Region,
        0);

    /// <summary>
    ///     按标准布局的 Group 顺序排列，用于构建工作区定义。
    /// </summary>
    public static readonly IReadOnlyList<ShowcasePanelDescriptor> All =
        new[] { Catalog, Layout, Content, DragDrop, Theme, Splitter, Keyboard, Persistence };

    /// <summary>
    ///     按教学序号排列，用于目录与快捷面板。
    /// </summary>
    public static readonly IReadOnlyList<ShowcasePanelDescriptor> CatalogOrder =
        All.OrderBy(panel => panel.Index, StringComparer.Ordinal).ToArray();

    public static IEnumerable<ShowcasePanelDescriptor> ForGroup(LayoutNodeId groupId)
        => All.Where(panel => panel.DefaultGroupId == groupId)
            .OrderBy(panel => panel.GroupOrder);
}

/// <summary>
///     示例文档标签页的单一元数据源。
/// </summary>
public static class ShowcaseDocuments
{
    public static readonly ShowcaseDocumentDescriptor Guide = new(
        "guide",
        "使用指南",
        ShowcaseContentKinds.Guide,
        new DockItemId("showcase-guide"),
        "showcase-guide",
        IsPinned: true);

    public static readonly ShowcaseDocumentDescriptor Shortcuts = new(
        "shortcuts",
        "快捷键速查",
        ShowcaseContentKinds.Guide,
        new DockItemId("showcase-shortcuts"),
        "showcase-shortcuts",
        IsPinned: false);

    public static readonly IReadOnlyList<ShowcaseDocumentDescriptor> All =
        new[] { Guide, Shortcuts };
}
