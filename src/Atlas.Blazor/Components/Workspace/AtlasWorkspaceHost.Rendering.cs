using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     <see cref="AtlasWorkspaceHost"/> 的渲染投影（partial）：
///     工作区根、分割节点、组、工具栏与固定槽位。
/// </summary>
public sealed partial class AtlasWorkspaceHost
{
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        routeCollector = new AtlasContentRouteCollector();
        if (ContentRoutes is not null)
        {
            builder.OpenComponent<CascadingValue<AtlasContentRouteCollector>>(0);
            // 收集器是每次渲染的注册载体：后代 AtlasContentRoute 组件
            // 必须在每次父渲染时重新注册，
            // 因此 CascadingValue 的键随收集器实例变化。
            // 下方的 workspace 根 div 反而携带 STABLE 键，
            // 因此 Blazor 会在重渲染间复用该 DOM 节点，
            // 交互控制器的监听器也保持绑定在存活的元素上。
            builder.SetKey(routeCollector);
            builder.AddAttribute(1, "Value", routeCollector);
            builder.AddAttribute(2, "ChildContent", ContentRoutes);
            builder.CloseComponent();
        }

        if (declarativeMode && WorkspaceId is not null)
        {
            collector = new AtlasDefinitionCollector();
            builder.OpenComponent<CascadingValue<AtlasDefinitionCollector>>(3);
            builder.SetKey(collector);
            builder.AddAttribute(4, "Value", collector);
            builder.AddAttribute(5, "ChildContent", ChildContent);
            builder.CloseComponent();
        }

        if (activeWorkspace is null)
        {
            RenderHostError(builder);
            return;
        }

        var snapshot = activeWorkspace.Current;
        // 交互控制器的稳定锚点：Blazor 会在重渲染间复用此 DOM 节点
        // （只有属性变化），因此控制器的指针监听器
        // 保持绑定在存活的元素上。
        builder.OpenElement(6, "div");
        builder.SetKey("atlas-workspace-root");
        builder.AddAttribute(7, "class", "atlas-v2-workspace");
        builder.AddAttribute(8, "role", "region");
        builder.AddAttribute(9, "aria-label", $"Atlas workspace {activeWorkspace.Id.Value}");
        builder.AddAttribute(10, "data-atlas-host-id", hostId);
        builder.AddAttribute(11, "data-atlas-protocol-version", "1");
        builder.AddAttribute(12, "data-atlas-workspace-id", activeWorkspace.Id.Value);
        builder.AddAttribute(
            13,
            "data-atlas-revision",
            snapshot.Revision.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(14, "data-atlas-direction", "inherit");
        builder.AddAttribute(15, "data-atlas-reduced-motion", "system");
        builder.AddElementReferenceCapture(16, element => rootElement = element);
        builder.OpenElement(17, "div");
        builder.AddAttribute(18, "class", "atlas-v2-overlay-root");
        builder.AddAttribute(19, "data-atlas-overlay-root", "true");
        builder.AddAttribute(20, "data-atlas-host-id", hostId);
        builder.AddAttribute(21, "aria-hidden", "true");
        builder.CloseElement();

        RenderFixedSlot(builder, "top-toolbar", TopToolbarContent);
        RenderHostError(builder);

        builder.OpenElement(22, "div");
        builder.AddAttribute(23, "class", "atlas-v2-work-area");
        builder.OpenRegion(24);
        RenderToolBar(builder, snapshot, LogicalSide.InlineStart);
        builder.CloseRegion();

        builder.OpenRegion(25);
        if (snapshot.RootNodeId is null)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "atlas-v2-workspace-empty");
            builder.AddContent(2, "No layout content");
            builder.CloseElement();
        }
        else
        {
            RenderNode(builder, snapshot, snapshot.RootNodeId.Value);
        }

        builder.CloseRegion();

        builder.OpenRegion(26);
        RenderToolBar(builder, snapshot, LogicalSide.InlineEnd);
        builder.CloseRegion();
        builder.CloseElement();
        RenderFixedSlot(builder, "status-bar", StatusBarContent);
        builder.CloseElement();
    }

    private void RenderNode(
        RenderTreeBuilder builder,
        WorkspaceSnapshot snapshot,
        LayoutNodeId nodeId)
    {
        // 渲染树是 JS 交互控制器的数据契约：data-atlas-* 属性承载
        // 分割缩放、折叠与拖拽所需的状态，属性名与值格式由交互脚本固定。
        var node = snapshot.Nodes[nodeId];
        if (node is SplitNode split)
        {
            var firstHidden = IsSubtreeEffectivelyHidden(snapshot, split.FirstId);
            var secondHidden = IsSubtreeEffectivelyHidden(snapshot, split.SecondId);
            var presentation = ResolveSplitterPresentation(split.Id);
            builder.OpenElement(0, "div");
            builder.SetKey(split.Id);
            builder.AddAttribute(1, "class", SplitClass(split.Axis));
            builder.AddAttribute(
                2,
                "data-atlas-split-hidden",
                firstHidden && secondHidden ? "both" : firstHidden ? "first" : secondHidden ? "second" : "none");
            builder.AddAttribute(3, "data-atlas-node-id", split.Id.Value);
            builder.AddAttribute(4, "data-atlas-node-kind", "split");
            builder.AddAttribute(5, "data-atlas-split-axis", split.Axis.ToString());
            builder.AddAttribute(
                6,
                "data-atlas-split-locked",
                presentation.IsLocked ? "true" : "false");
            builder.AddAttribute(7, "data-atlas-split-ratio", SplitRatio(split.Basis));
            builder.AddAttribute(
                8,
                "data-atlas-split-basis-kind",
                split.Basis.Kind.ToString());
            builder.AddAttribute(
                9,
                "data-atlas-split-basis-value",
                Invariant(split.Basis.Value));
            if (split.Basis.Anchor is not null)
            {
                builder.AddAttribute(
                    10,
                    "data-atlas-split-anchor",
                    split.Basis.Anchor.Value.ToString());
            }

            builder.AddAttribute(
                11,
                "data-atlas-revision",
                snapshot.Revision.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                12,
                "data-atlas-first-minimum",
                Invariant(split.Constraints.FirstMinimum));
            builder.AddAttribute(
                13,
                "data-atlas-second-minimum",
                Invariant(split.Constraints.SecondMinimum));
            AddOptionalAttribute(
                builder,
                14,
                "data-atlas-first-maximum",
                split.Constraints.FirstMaximum);
            AddOptionalAttribute(
                builder,
                15,
                "data-atlas-second-maximum",
                split.Constraints.SecondMaximum);
            // 折叠侧仍保留在 DOM 中（仅样式隐藏），
            // 因此用 0px/1fr 覆盖比例样式，而非省略渲染或沿用基础比例。
            builder.AddAttribute(
                16,
                "style",
                firstHidden || secondHidden
                    ? CollapseAdjustedStyle(firstHidden, secondHidden)
                    : SplitStyle(split.Basis));
            RenderNode(builder, snapshot, split.FirstId);
            builder.OpenElement(17, "div");
            builder.AddAttribute(
                18,
                "class",
                CssClasses("atlas-v2-splitter", presentation.CssClass));
            builder.AddAttribute(
                19,
                "data-atlas-splitter-hidden",
                firstHidden || secondHidden ? "true" : "false");
            builder.AddAttribute(20, "data-atlas-split-id", split.Id.Value);
            builder.AddAttribute(21, "role", "separator");
            builder.AddAttribute(22, "aria-label", $"Resize split {split.Id.Value}");
            builder.AddAttribute(23, "tabindex", presentation.IsLocked ? "-1" : "0");
            builder.AddAttribute(
                24,
                "aria-orientation",
                split.Axis == SplitAxis.InlineChildren ? "vertical" : "horizontal");
            builder.AddAttribute(
                25,
                "aria-disabled",
                presentation.IsLocked ? "true" : "false");
            builder.AddAttribute(26, "aria-valuemin", "0");
            builder.AddAttribute(27, "aria-valuemax", "100");
            builder.AddAttribute(
                28,
                "aria-valuenow",
                (double.Parse(
                    SplitRatio(split.Basis),
                    CultureInfo.InvariantCulture) * 100d)
                    .ToString("R", CultureInfo.InvariantCulture));
            builder.AddAttribute(
                29,
                "data-atlas-can-collapse-first",
                presentation.CanCollapseFirst ? "true" : "false");
            builder.AddAttribute(
                30,
                "data-atlas-can-collapse-second",
                presentation.CanCollapseSecond ? "true" : "false");
            RenderCollapseButton(
                builder,
                snapshot,
                split.FirstId,
                "first",
                presentation.CanCollapseFirst);
            RenderCollapseButton(
                builder,
                snapshot,
                split.SecondId,
                "second",
                presentation.CanCollapseSecond);
            builder.CloseElement();
            RenderNode(builder, snapshot, split.SecondId);
            builder.CloseElement();
            return;
        }

        var group = (GroupNode)node;
        var items = group.ItemIds.Select(itemId => snapshot.Items[itemId]).ToArray();
        var emptyTemplate = group.Kind == DockItemKind.Tool
            ? ToolEmptyTemplate
            : DocumentEmptyTemplate;
        var headerTemplate = group.Kind == DockItemKind.Tool
            ? ToolHeaderTemplate
            : DocumentHeaderTemplate;
        builder.OpenComponent<AtlasContentErrorBoundary>(0);
        builder.SetKey(group.Id);
        builder.AddAttribute(
            1,
            "SafeError",
            new AtlasHostError(
                AtlasHostErrorCode.Unexpected,
                "ATLAS-GROUP-RENDER-FAILED",
                "Atlas.Group.RenderFailed",
                activeWorkspace!.Id,
                nodeId: group.Id));
        builder.AddAttribute(2, "OnSafeError", OnError);
        builder.AddAttribute(
            3,
            "RecoveryKey",
            (group.Id, emptyTemplate, headerTemplate));
        builder.AddAttribute(
            4,
            "ErrorContent",
            (RenderFragment<Exception>)(_ => errorBuilder =>
            {
                errorBuilder.OpenElement(0, "div");
                errorBuilder.AddAttribute(1, "class", "atlas-v2-group-error");
                errorBuilder.AddContent(2, "Workspace group could not be rendered.");
                errorBuilder.CloseElement();
            }));
        builder.AddAttribute(
            5,
            "ChildContent",
            (RenderFragment)(groupBuilder =>
            {
                groupBuilder.OpenComponent<AtlasGroupView>(0);
                groupBuilder.SetKey(group.Id);
                groupBuilder.AddAttribute(1, "Workspace", activeWorkspace);
                groupBuilder.AddAttribute(2, "Snapshot", snapshot);
                groupBuilder.AddAttribute(3, "Group", group);
                groupBuilder.AddAttribute(4, "Items", items);
                groupBuilder.AddAttribute(5, "Catalog", effectiveCatalog ?? AtlasContentCatalog.Empty);
                groupBuilder.AddAttribute(6, "FocusCoordinator", focusCoordinator);
                groupBuilder.AddAttribute(7, "EmptyTemplate", emptyTemplate);
                groupBuilder.AddAttribute(8, "HeaderTemplate", headerTemplate);
                groupBuilder.AddAttribute(9, "OnError", OnError);
                groupBuilder.AddAttribute(10, "ContentLifetimeOptions", ContentLifetimeOptions);
                groupBuilder.AddAttribute(11, "SandboxedFrameResolver", SandboxedFrameResolver);
                groupBuilder.AddAttribute(12, "Generation", interactionGeneration);
                groupBuilder.AddAttribute(13, "HostId", hostId);
                groupBuilder.AddAttribute(14, "StateScope", effectiveStateScope);
                groupBuilder.CloseComponent();
            }));
        builder.CloseComponent();
    }

    private void RenderToolBar(
        RenderTreeBuilder builder,
        WorkspaceSnapshot snapshot,
        LogicalSide side)
    {
        if (snapshot.ToolBars.TryGetValue(side, out var toolBar)
            && toolBar.Visibility == ToolBarVisibility.Collapsed)
        {
            return;
        }

        var regions = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool
                && group.LogicalRegion is not null
                && AtlasDefinitionCollector.SideFor(group.LogicalRegion.Value) == side)
            .OrderBy(group => group.LogicalRegion)
            .Select(
                group => (Group: group, Items: OrderedItems(snapshot, group).ToArray()))
            .ToArray();
        if (regions.Length == 0)
        {
            return;
        }

        var upperRegions = regions
            .Where(region => IsUpperOrLowerRegion(region.Group.LogicalRegion!.Value))
            .ToArray();
        var bottomRegions = regions
            .Where(region => IsBottomRegion(region.Group.LogicalRegion!.Value))
            .ToArray();

        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", "atlas-v2-toolbar");
        builder.AddAttribute(2, "role", "tablist");
        builder.AddAttribute(3, "aria-orientation", "vertical");
        builder.AddAttribute(
            4,
            "aria-label",
            side == LogicalSide.InlineStart ? "Inline start tools" : "Inline end tools");
        builder.AddAttribute(
            5,
            "data-atlas-toolbar-side",
            side == LogicalSide.InlineStart ? "inline-start" : "inline-end");
        var activeItemId = regions
            .Where(region => region.Group.Visibility == GroupVisibility.Expanded)
            .Select(region => region.Group.SelectedItemId)
            .FirstOrDefault(itemId => itemId is not null);
        // 漫游焦点项：展开组的选中项，无展开组时退回首个工具项，作为标签 tabindex 定位点。
        var rovingItemId = activeItemId
            ?? regions
                .SelectMany(region => region.Items)
                .Select(item => item.Id)
                .FirstOrDefault();

        var hasUpperContent = upperRegions.Any(region => region.Items.Length != 0);
        var hasBottomContent = bottomRegions.Any(region => region.Items.Length != 0);

        if (upperRegions.Length != 0)
        {
            builder.OpenElement(6, "section");
            builder.AddAttribute(1, "class", "atlas-v2-toolbar-area");
            builder.AddAttribute(2, "data-atlas-toolbar-area", "upper");
            for (var regionIndex = 0; regionIndex < upperRegions.Length; regionIndex++)
            {
                if (regionIndex != 0)
                {
                    builder.OpenElement(3, "div");
                    builder.AddAttribute(4, "class", "atlas-v2-toolbar-divider");
                    builder.AddAttribute(5, "role", "separator");
                    builder.CloseElement();
                }

                // 集合区域复用相同的序列槽位；
                // 区域数量由声明固定，因此顺序 diff 会按序匹配它们。
                builder.OpenRegion(6);
                RenderToolBarRegion(builder, upperRegions[regionIndex], rovingItemId);
                builder.CloseRegion();
            }

            builder.CloseElement();
        }

        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "class", "atlas-v2-toolbar-divider atlas-v2-toolbar-section-divider");
        builder.AddAttribute(9, "role", "separator");
        builder.AddAttribute(
            10,
            "data-atlas-toolbar-section-divider",
            hasUpperContent && hasBottomContent ? "true" : "false");
        builder.CloseElement();

        if (bottomRegions.Length != 0)
        {
            builder.OpenElement(11, "section");
            builder.AddAttribute(1, "class", "atlas-v2-toolbar-area atlas-v2-toolbar-area-bottom");
            builder.AddAttribute(2, "data-atlas-toolbar-area", "bottom");
            for (var regionIndex = 0; regionIndex < bottomRegions.Length; regionIndex++)
            {
                if (regionIndex != 0)
                {
                    builder.OpenElement(3, "div");
                    builder.AddAttribute(4, "class", "atlas-v2-toolbar-divider");
                    builder.AddAttribute(5, "role", "separator");
                    builder.CloseElement();
                }

                builder.OpenRegion(6);
                RenderToolBarRegion(builder, bottomRegions[regionIndex], rovingItemId);
                builder.CloseRegion();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderToolBarRegion(
        RenderTreeBuilder builder,
        (GroupNode Group, DockItem[] Items) region,
        DockItemId rovingItemId)
    {
        builder.OpenElement(0, "section");
        builder.SetKey(region.Group.Id);
        builder.AddAttribute(1, "class", "atlas-v2-toolbar-region");
        builder.AddAttribute(
            2,
            "data-atlas-toolbar-region",
            region.Group.LogicalRegion!.Value.ToString());
        builder.AddAttribute(
            3,
            "data-atlas-group-id",
            region.Group.Id.Value);
        builder.AddAttribute(
            4,
            "data-atlas-region-empty",
            region.Items.Length == 0 ? "true" : "false");
        for (var itemIndex = 0; itemIndex < region.Items.Length; itemIndex++)
        {
            var item = region.Items[itemIndex];
            var isActive = region.Group.Visibility == GroupVisibility.Expanded
                && region.Group.SelectedItemId == item.Id;
            // 集合项在每次迭代中复用相同的序列组，
            // 并通过 SetKey 区分。
            builder.OpenElement(0, "button");
            builder.SetKey(item.Id);
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "role", "tab");
            builder.AddAttribute(
                3,
                "id",
                AtlasDomIdentity.ControlId(hostId, item.Id, "tool-tab"));
            builder.AddAttribute(4, "aria-label", item.Title);
            builder.AddAttribute(5, "aria-selected", isActive);
            builder.AddAttribute(
                6,
                "aria-controls",
                AtlasDomIdentity.PanelId(hostId, item.Id));
            builder.AddAttribute(7, "tabindex", item.Id == rovingItemId ? "0" : "-1");
            builder.AddAttribute(8, "data-atlas-item-id", item.Id.Value);
            builder.AddAttribute(9, "data-atlas-item-kind", "tool");
            builder.AddAttribute(10, "data-atlas-drag-kind", "toolbar-entry");
            builder.AddAttribute(11, "data-atlas-toolbar-entry", "true");
            builder.AddAttribute(12, "data-atlas-group-id", region.Group.Id.Value);
            builder.AddAttribute(
                13,
                "data-atlas-toolbar-region",
                region.Group.LogicalRegion.Value.ToString());
            builder.AddAttribute(
                14,
                "data-atlas-insertion-index",
                itemIndex.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                15,
                "data-atlas-command-capabilities",
                ((int)item.Capabilities).ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(
                16,
                "onclick",
                EventCallback.Factory.Create(
                    this,
                    () => ToggleToolAsync(region.Group, item.Id, isActive)));
            builder.OpenRegion(17);
            if (item.IconKey is not null)
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "atlas-v2-item-icon");
                builder.AddAttribute(2, "data-atlas-icon", item.IconKey);
                builder.AddAttribute(3, "aria-hidden", "true");
                builder.CloseElement();
            }

            builder.CloseRegion();
            builder.AddContent(18, item.Title);
            builder.CloseElement();
        }

        builder.OpenElement(19, "div");
        builder.AddAttribute(20, "class", "atlas-v2-drop-scaffold");
        builder.AddAttribute(21, "role", "presentation");
        builder.AddAttribute(22, "aria-hidden", "true");
        builder.AddAttribute(
            23,
            "data-atlas-drop-scaffold",
            "true");
        builder.AddAttribute(24, "data-atlas-group-id", region.Group.Id.Value);
        builder.AddAttribute(
            25,
            "data-atlas-toolbar-region",
            region.Group.LogicalRegion.Value.ToString());
        builder.AddAttribute(
            26,
            "data-atlas-insertion-index",
            region.Items.Length.ToString(CultureInfo.InvariantCulture));
        builder.CloseElement();

        builder.CloseElement();
    }

    private static bool IsUpperOrLowerRegion(LogicalRegion region)
    {
        return region is LogicalRegion.InlineStartUpper
            or LogicalRegion.InlineStartLower
            or LogicalRegion.InlineEndUpper
            or LogicalRegion.InlineEndLower;
    }

    private static bool IsBottomRegion(LogicalRegion region)
    {
        return region is LogicalRegion.BlockEndInlineStart
            or LogicalRegion.BlockEndInlineEnd;
    }

    private void RenderFixedSlot(
        RenderTreeBuilder builder,
        string slot,
        RenderFragment? content)
    {
        if (content is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", $"atlas-v2-{slot}");
        builder.AddAttribute(2, "data-atlas-fixed-slot", slot);
        builder.AddContent(3, content);
        builder.CloseElement();
    }

    private void RenderHostError(RenderTreeBuilder builder)
    {
        if (hostError is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "atlas-v2-host-error");
        builder.AddAttribute(2, "data-atlas-error-code", hostError.Code.ToString());
        if (ErrorTemplate is not null)
        {
            builder.AddContent(3, ErrorTemplate(hostError));
        }
        else
        {
            builder.AddContent(3, "Atlas could not apply the requested layout.");
        }

        builder.CloseElement();
    }

    private static IEnumerable<DockItem> OrderedItems(
        WorkspaceSnapshot snapshot,
        GroupNode group)
    {
        var ids = group.LogicalRegion is LogicalRegion region
            && snapshot.ToolBarOrders.TryGetValue(region, out var order)
            ? order.ItemIds
            : group.ItemIds;
        return ids.Select(itemId => snapshot.Items[itemId]);
    }

    private void RenderCollapseButton(
        RenderTreeBuilder builder,
        WorkspaceSnapshot snapshot,
        LayoutNodeId childId,
        string childName,
        bool enabled)
    {
        // 折叠目标必须是组节点（分割子节点没有可见性概念），
        // 可用性由分割条呈现能力位决定；按钮状态派生自快照的组可见性，
        // 操作提交并重渲染后自然回到一致状态，无需本地状态同步。
        if (!enabled
            || !snapshot.Nodes.TryGetValue(childId, out var node)
            || node is not GroupNode group)
        {
            return;
        }

        var collapsed = group.Visibility == GroupVisibility.Collapsed;
        builder.OpenElement(0, "button");
        builder.SetKey((group.Id, "split-collapse"));
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "atlas-v2-splitter-collapse");
        builder.AddAttribute(3, "data-atlas-collapse-child", childName);
        builder.AddAttribute(
            4,
            "aria-label",
            collapsed ? "Expand region" : "Collapse region");
        builder.AddAttribute(
            5,
            "onclick",
            EventCallback.Factory.Create(
                this,
                () => ToggleSplitChildAsync(group.Id, collapsed)));
        builder.AddContent(6, collapsed ? "›" : "‹");
        builder.CloseElement();
    }

    private static void AddOptionalAttribute(
        RenderTreeBuilder builder,
        int sequence,
        string name,
        double? value)
    {
        if (value is not null)
        {
            builder.AddAttribute(sequence, name, Invariant(value.Value));
        }
    }

}
