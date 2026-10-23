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

/// <summary>
///     将单个组节点投影为组区域：文档组渲染标签条、工具组渲染面板头部，
///     并渲染选中项与 keep-alive 非选中项的内容出口。
/// </summary>
internal sealed class AtlasGroupView : ComponentBase
{
    // 选中项内容出口的引用；焦点仲裁成功后经它聚焦。
    private AtlasContentOutlet? selectedContentOutlet;

    // 上次实际渲染时的参数戳；ShouldRender 与当前戳比较。
    private int lastRenderedStamp;

    // 本次参数设置汇总出的参数戳；变化才触发重渲染。
    private int currentStamp;

    // 已上报的缺失/冲突内容身份；同一状态只报告一次。
    private string? reportedMissingContent;

    // keep-alive 缓存中保持挂载的非选中项；按最近活动排序，超出上限时淘汰最旧。
    private readonly List<DockItemId> keptAliveItemIds = new();

    /// <summary>
    ///     组所属的工作区；选择操作经它提交。
    /// </summary>
    [Parameter]
    public IAtlasWorkspace Workspace { get; set; } = null!;

    /// <summary>
    ///     获取或设置本次渲染所基于的不可变工作区快照。
    /// </summary>
    [Parameter]
    public WorkspaceSnapshot Snapshot { get; set; } = null!;

    /// <summary>
    ///     获取或设置本视图投影的组节点。
    /// </summary>
    [Parameter]
    public GroupNode Group { get; set; } = null!;

    /// <summary>
    ///     获取或设置组内按声明顺序排列的项集合。
    /// </summary>
    [Parameter]
    public IReadOnlyList<DockItem> Items { get; set; } = Array.Empty<DockItem>();

    /// <summary>
    ///     解析内容种类的生效目录。
    /// </summary>
    [Parameter]
    public AtlasContentCatalog Catalog { get; set; } = null!;

    /// <summary>
    ///     获取或设置焦点仲裁器；渲染完成后经其认领才聚焦选中项。
    /// </summary>
    [Parameter]
    public AtlasFocusCoordinator FocusCoordinator { get; set; } = null!;

    /// <summary>
    ///     获取或设置组空状态模板；为 <see langword="null"/> 时显示内置空文本。
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasEmptyStateContext>? EmptyTemplate { get; set; }

    /// <summary>
    ///     项标题模板，同时服务文档标签页与工具面板头部；
    ///     为 <see langword="null"/> 时显示项标题。
    /// </summary>
    [Parameter]
    public RenderFragment<AtlasItemHeaderContext>? HeaderTemplate { get; set; }

    /// <summary>
    ///     获取或设置内容解析失败与聚焦失败时的安全宿主错误回调。
    /// </summary>
    [Parameter]
    public EventCallback<AtlasHostError> OnError { get; set; }

    /// <summary>
    ///     获取或设置内容生命周期策略；非默认模式时对非选中项做 keep-alive 缓存。
    /// </summary>
    [Parameter]
    public WorkspaceContentLifetimeOptions ContentLifetimeOptions { get; set; } = new();

    /// <summary>
    ///     获取或设置显式不可信内容解析器；非 null 结果渲染沙箱 iframe 而非受信组件路径。
    /// </summary>
    [Parameter]
    public Func<AtlasContentContext, AtlasSandboxedFrameOptions?>? SandboxedFrameResolver { get; set; }

    /// <summary>
    ///     获取或设置内容代际：代际变化强制重渲染并重建内容投影。
    /// </summary>
    [Parameter]
    public long Generation { get; set; }

    /// <summary>
    ///     宿主标识；组内标签页、面板与标题的稳定 DOM id 由它派生。
    /// </summary>
    [Parameter]
    public string HostId { get; set; } = string.Empty;

    /// <summary>
    ///     获取或设置内容状态作用域：为组内各条目提供隔离的业务状态容器。
    /// </summary>
    [Parameter]
    public IAtlasContentStateScope? StateScope { get; set; }

    protected override void OnParametersSet()
    {
        UpdateLifetimeCache();
        // 汇总影响投影的参数生成渲染戳，ShouldRender 据此跳过无关重渲染。
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Group));
        foreach (var item in Items)
        {
            hash.Add(RuntimeHelpers.GetHashCode(item));
        }

        hash.Add(EmptyTemplate);
        hash.Add(HeaderTemplate);
        hash.Add(FocusCoordinator.Version);
        hash.Add(ContentLifetimeOptions.Mode);
        hash.Add(ContentLifetimeOptions.MaxCachedItems);
        hash.Add(SandboxedFrameResolver);
        hash.Add(Catalog);
        hash.Add(Generation);
        hash.Add(StateScope);
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
        builder.OpenElement(0, "section");
        builder.SetKey(Group.Id);
        builder.AddAttribute(1, "class", GroupClass());
        builder.AddAttribute(2, "data-atlas-node-id", Group.Id.Value);
        builder.AddAttribute(3, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(4, "data-atlas-group-kind", Group.Kind.ToString().ToLowerInvariant());
        builder.AddAttribute(5, "role", "region");
        if (Group.SelectedItemId is not null)
        {
            var selected = Items.First(item => item.Id == Group.SelectedItemId.Value);
            builder.AddAttribute(
                6,
                "aria-labelledby",
                Group.Kind == DockItemKind.Document
                    ? AtlasDomIdentity.ControlId(HostId, selected.Id, "document-tab")
                    : AtlasDomIdentity.HeadingId(HostId, selected.Id));
        }
        if (Group.ToolbarRegion is not null)
        {
            builder.AddAttribute(
                7,
                "data-atlas-logical-region",
                Group.ToolbarRegion.Value.ToString());
        }

        if (Items.Count == 0)
        {
            RenderEmpty(builder);
        }
        else
        {
            if (Group.Kind == DockItemKind.Document)
            {
                builder.AddContent(
                    8,
                    (RenderFragment)(headerBuilder =>
                    {
                        RenderTabStrip(headerBuilder);
                    }));
            }
            else
            {
                builder.AddContent(
                    8,
                    (RenderFragment)(headerBuilder =>
                    {
                        RenderToolHeader(headerBuilder);
                    }));
            }

            builder.AddContent(
                9,
                (RenderFragment)(contentBuilder =>
                {
                    RenderContentOutlets(contentBuilder);
                }));
        }

        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 先记录已渲染戳再执行副作用：ShouldRender 依赖戳值判断参数是否变化，
        // 必须在本次渲染完成后更新，避免把「尚未渲染」误判为「无需渲染」。
        lastRenderedStamp = currentStamp;
        await ReportMissingContentAsync();
        // 焦点经协调器仲裁：同一渲染批次中多个组都可能请求聚焦，
        // 只有成功认领的组才调用 FocusAsync，防止焦点在组间来回跳动。
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
        if (Group.SelectedItemId is null)
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

        // 解析失败报告必须去重：此方法随每次渲染执行，
        // 以项+内容种类+错误码为身份只报告一次，状态变化或恢复后重新报告。
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

    private void RenderEmpty(RenderTreeBuilder builder)
    {
        var context = new AtlasEmptyStateContext(
            Workspace.Id,
            Group.Id,
            Group.Kind,
            Group.RetentionPolicy);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-empty-state");
        if (EmptyTemplate is not null)
        {
            builder.AddContent(2, EmptyTemplate(context));
        }
        else
        {
            builder.AddContent(
                2,
                Group.Kind == DockItemKind.Tool
                    ? "No tool content"
                    : "No document content");
        }

        builder.CloseElement();
    }

    private void RenderTabStrip(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-tab-strip");
        builder.AddAttribute(2, "role", "tablist");
        builder.AddAttribute(3, "aria-orientation", "horizontal");
        builder.AddAttribute(4, "aria-label", $"{Group.Id.Value} documents");
        builder.AddAttribute(5, "data-atlas-group-id", Group.Id.Value);
        for (var itemIndex = 0; itemIndex < Items.Count; itemIndex++)
        {
            var item = Items[itemIndex];
            var selected = item.Id == Group.SelectedItemId;
            // 集合项每次迭代复用同一序列组，靠 SetKey 区分；
            // 这样插入/移除标签页不会改变后续标签页的 diff 序列。
            builder.OpenElement(0, "button");
            builder.SetKey(item.Id);
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "role", "tab");
            builder.AddAttribute(
                3,
                "id",
                AtlasDomIdentity.ControlId(HostId, item.Id, "document-tab"));
            builder.AddAttribute(4, "aria-label", item.Title);
            builder.AddAttribute(
                5,
                "aria-controls",
                AtlasDomIdentity.PanelId(HostId, item.Id));
            builder.AddAttribute(6, "data-atlas-item-id", item.Id.Value);
            builder.AddAttribute(7, "data-atlas-item-kind", "document");
            builder.AddAttribute(8, "data-atlas-drag-kind", "document-tab");
            builder.AddAttribute(9, "data-atlas-group-id", Group.Id.Value);
            builder.AddAttribute(
                10,
                "data-atlas-insertion-index",
                itemIndex.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                11,
                "data-atlas-command-capabilities",
                ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(12, "aria-selected", selected);
            builder.AddAttribute(13, "tabindex", selected ? "0" : "-1");
            builder.AddAttribute(
                14,
                "onclick",
                EventCallback.Factory.Create(this, () => SelectAsync(item.Id)));
            RenderHeader(builder, item, selected);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderToolHeader(RenderTreeBuilder builder)
    {
        // 工具组头部只呈现选中项：与文档标签条不同，工具面板
        // 同一时刻只有一个可见，头部兼作标题与拖拽手柄。
        var selected = SelectedItem();
        builder.OpenElement(0, "header");
        builder.AddAttribute(1, "class", "atlas-v2-panel-header");
        builder.AddAttribute(2, "role", "heading");
        builder.AddAttribute(3, "aria-level", "2");
        builder.AddAttribute(
            4,
            "id",
            AtlasDomIdentity.HeadingId(HostId, selected.Id));
        builder.AddAttribute(5, "data-atlas-panel-header", "true");
        builder.AddAttribute(6, "data-atlas-drag-kind", "panel-header");
        builder.AddAttribute(7, "data-atlas-item-id", selected.Id.Value);
        builder.AddAttribute(8, "data-atlas-item-kind", "tool");
        builder.AddAttribute(9, "data-atlas-group-id", Group.Id.Value);
        builder.AddAttribute(
            10,
            "data-atlas-command-capabilities",
            ((int)selected.Capabilities).ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(
            11,
            "data-atlas-insertion-index",
            Group.ItemIds.ToList().IndexOf(selected.Id).ToString(CultureInfo.InvariantCulture));
        RenderHeader(builder, selected, isSelected: true);
        builder.CloseElement();
    }

    private void RenderHeader(
        RenderTreeBuilder builder,
        DockItem item,
        bool isSelected)
    {
        if (HeaderTemplate is not null)
        {
            builder.AddContent(
                0,
                HeaderTemplate(new AtlasItemHeaderContext(item, isSelected)));
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

    private void RenderContentOutlets(RenderTreeBuilder builder)
    {
        var selectedId = Group.SelectedItemId
            ?? throw new InvalidOperationException("A non-empty group must select an item.");
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-content-stack");
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
        var context = new AtlasContentContext(Workspace, item, StateScope);
        var resolution = Catalog.TryResolve(item.Content.Kind, out var descriptor);
        // 键由项 ID 与描述符修订号组成：注册结构变化（组件类型或绑定参数）
        // 使修订号递增并重建出口，仅值更新则保留组件实例与状态。
        builder.OpenComponent<AtlasContentOutlet>(0);
        builder.SetKey((item.Id, descriptor?.Revision ?? 0));
        builder.AddAttribute(1, "Content", context);
        builder.AddAttribute(2, "Resolution", resolution);
        builder.AddAttribute(3, "Descriptor", descriptor);
        builder.AddAttribute(4, "IsSelected", isSelected);
        builder.AddAttribute(5, "PanelId", AtlasDomIdentity.PanelId(HostId, item.Id));
        builder.AddAttribute(
            6,
            "LabelledById",
            AtlasDomIdentity.ControlId(
                HostId,
                item.Id,
                Group.Kind == DockItemKind.Document ? "document-tab" : "tool-tab"));
        // 恢复键包含沙箱解析器与注册修订号：任一变化都会让错误边界
        // 清除旧内容的错误状态，避免把上一次内容的失败投射到新内容上。
        builder.AddAttribute(
            7,
            "RecoveryKey",
            (SandboxedFrameResolver, descriptor?.Revision ?? 0));
        builder.AddAttribute(8, "OnError", OnError);
        builder.AddAttribute(
            9,
            "SandboxedFrameResolver",
            SandboxedFrameResolver);
        builder.AddAttribute(10, "Generation", Generation);
        builder.AddComponentReferenceCapture(
            11,
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
