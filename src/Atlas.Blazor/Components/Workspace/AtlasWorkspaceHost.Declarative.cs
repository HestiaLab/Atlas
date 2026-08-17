using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     <see cref="AtlasWorkspaceHost"/> 的声明式提交逻辑（partial）：
///     构建声明定义、合并运行时项并对账提交；暂存并原子提交 Route/Map 源生成。
/// </summary>
public sealed partial class AtlasWorkspaceHost
{
    private async Task ProcessDeclarationAsync()
    {
        // 声明收集器在渲染期间经 CascadingValue 由子组件填充，
        // 因此声明处理延后到 OnAfterRenderAsync，此处才拥有完整声明。
        if (!declarativeMode || disposed || WorkspaceId is null)
        {
            return;
        }

        if (!collector.TryBuild(WorkspaceId.Value, out var declaration)
            || declaration is null)
        {
            // 无效声明的指纹哨兵；与失败关联使同一错误只上报一次。
            var fingerprint = "invalid";
            if (!string.Equals(processedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                processedFingerprint = fingerprint;
                await ReportErrorAsync(DefinitionError(WorkspaceId));
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        var currentPresentationFingerprint = PresentationFingerprint(collector);
        var presentationChanged = !string.Equals(
            presentationFingerprint,
            currentPresentationFingerprint,
            StringComparison.Ordinal);
        presentationFingerprint = currentPresentationFingerprint;
        declarativeSplitterPresentations = collector.GetSplitPresentations();
        // 布局指纹未变时声明内容等价，跳过创建/对账以省去一次工作区操作；
        // 但分割条呈现变化仍需重渲染，以反映锁定与可折叠状态。
        var currentFingerprint = Fingerprint(declaration);
        if (string.Equals(processedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            if (presentationChanged)
            {
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        processedFingerprint = currentFingerprint;
        if (activeWorkspace is null)
        {
            var result = (Factory ?? new AtlasWorkspaceFactory()).Create(
                declaration,
                WorkspaceOptions);
            if (!result.Succeeded)
            {
                await ReportErrorAsync(DefinitionError(WorkspaceId));
            }
            else
            {
                await BindWorkspaceAsync(
                    result.Value!,
                    declarative: true,
                    ShouldOwnWorkspace(declarative: true));
                RememberDeclaration(declaration);
            }

            await InvokeAsync(StateHasChanged);
            return;
        }

        // 工作区已存在时不能直接对账新声明：程序化添加的运行时项不在声明中，
        // 直接重建会被丢弃，因此先合并运行时项再对账。
        if (!TryMergeRuntimeItems(declaration, out var merged))
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reconcile = await activeWorkspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                merged!,
                activeWorkspace.Current.Revision));
        if (reconcile.Status == WorkspaceResultStatus.Failure)
        {
            await ReportErrorAsync(DefinitionError(WorkspaceId));
        }
        else
        {
            hostError = null;
            RememberDeclaration(declaration);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ProcessContentSourcesAsync()
    {
        if (disposed)
        {
            return;
        }

        // 仅当父组件确实重新渲染了此宿主（OnParametersSet 已运行）时，
        // 才暂存 Route/Map 源生成。宿主内部重渲染会重新执行
        // 父组件的 ContentRoutes 片段，但没有新的参数值；
        // 再次处理它们会造成无限渲染循环。
        if (!parentContentGenerationSeen)
        {
            return;
        }

        parentContentGenerationSeen = false;

        if (!routeCollector.TryBuild(out var collectedRoutes))
        {
            // 出错或不完整的收集会保持先前已提交的 Route/Map 生成有效，
            // 并报告安全的定义错误。
            if (!routeFaultReported)
            {
                routeFaultReported = true;
                if (OnError.HasDelegate)
                {
                    await OnError.InvokeAsync(
                        new AtlasHostError(
                            AtlasHostErrorCode.DefinitionInvalid,
                            "ATLAS-CONTENT-ROUTE-INVALID",
                            "Atlas.Content.RouteInvalid",
                            activeWorkspace?.Id));
                }
            }

            return;
        }

        routeFaultReported = false;

        // 每个父渲染周期暂存一个候选的宿主本地源生成。
        // 候选在路由收集完成后原子提交：重建的 Catalog 引用会使组渲染戳失效，
        // 内容出口据此重新求值值工厂与当前路由委托。
        // 因此，仅值更新的投影在保留组件标识的同时，
        // 在后续投影中交付完整的绑定 Parameter 集。
        // 宿主内部重渲染永远不会到达此路径。
        var previous = committedContentGeneration;
        var stagedMap = ContentMap?.Entries ?? Array.Empty<AtlasContentMapEntry>();
        var generation = BuildContentGeneration(previous, collectedRoutes, stagedMap);
        committedContentGeneration = generation;
        effectiveCatalog = (Registry ?? AtlasContentCatalog.Empty).WithHostLocal(generation);
        await InvokeAsync(StateHasChanged);
    }

    private IReadOnlyList<AtlasContentHostLocalRegistration> BuildContentGeneration(
        IReadOnlyList<AtlasContentHostLocalRegistration> previous,
        IReadOnlyList<AtlasContentRouteDescriptor> collectedRoutes,
        IReadOnlyList<AtlasContentMapEntry> stagedMap)
    {
        // 上次提交的注册按 Kind 索引；同 Kind 同来源延续结构修订号。
        var previousByKind = previous
            .GroupBy(registration => registration.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var generation = new List<AtlasContentHostLocalRegistration>(
            collectedRoutes.Count + stagedMap.Count);
        foreach (var route in collectedRoutes)
        {
            // 相同的 Kind 与源所有者会延续结构修订号，
            // 仅值更新也能保留组件标识；
            // 结构变化（新增 Kind、移除 Kind 或所有者切换）会重新挂载内容出口。
            var revision = previousByKind.TryGetValue(route.Kind, out var previousRoute)
                && previousRoute.Source == AtlasContentRegistrationSource.Route
                ? previousRoute.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    route.Kind,
                    AtlasContentRegistrationSource.Route,
                    route.ChildContent!,
                    revision,
                    StructuralSignature: "Route"));
        }

        foreach (var entry in stagedMap)
        {
            // 映射结构签名（所有者、组件类型、有序绑定参数名）
            // 驱动修订号延续；值工厂及其结果永远不会被视为结构变化。
            var revision = previousByKind.TryGetValue(entry.Kind, out var previousEntry)
                && previousEntry.Source == AtlasContentRegistrationSource.Map
                && string.Equals(
                    previousEntry.StructuralSignature,
                    entry.StructuralSignature,
                    StringComparison.Ordinal)
                ? previousEntry.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    entry.Kind,
                    AtlasContentRegistrationSource.Map,
                    entry.CreateRenderer(),
                    revision,
                    entry.StructuralSignature));
        }

        return generation;
    }

    private bool TryMergeRuntimeItems(
        AtlasWorkspaceDefinition declaration,
        out AtlasWorkspaceDefinition? merged)
    {
        // 对账会按声明重建布局，未声明但仍在运行的项会被丢弃；
        // 此方法把它们并入新声明，并验证全部运行时项仍被放置且不与声明冲突。
        merged = null;
        if (activeWorkspace is null || previousDeclaration is null)
        {
            merged = declaration;
            return true;
        }

        var current = activeWorkspace.Current;
        // 未在声明中的运行时项；对账会丢弃它们，需先并入新声明。
        var runtimeIds = current.Items.Keys
            .Where(itemId => !declaredItemIds.Contains(itemId))
            .ToHashSet();
        var placedRuntimeIds = current.Nodes.Values
            .OfType<GroupNode>()
            .SelectMany(group => group.ItemIds)
            .Concat(
                current.AutoHideStrips.Values.SelectMany(
                    strip => strip.Entries.Select(entry => entry.ItemId)))
            .Where(runtimeIds.Contains)
            .Distinct()
            .ToArray();
        if (placedRuntimeIds.Length != runtimeIds.Count)
        {
            return false;
        }

        var runtimeItems = placedRuntimeIds
            .Select(itemId => current.Items[itemId])
            .ToArray();
        if (runtimeItems.Any(item => declaration.Items.Any(candidate => candidate.Id == item.Id)))
        {
            return false;
        }

        var runtimeByGroup = new Dictionary<LayoutNodeId, List<DockItem>>();
        foreach (var currentGroup in current.Nodes.Values.OfType<GroupNode>())
        {
            var groupRuntimeItems = currentGroup.ItemIds
                .Where(runtimeIds.Contains)
                .Select(itemId => current.Items[itemId])
                .ToList();
            if (groupRuntimeItems.Count == 0)
            {
                continue;
            }

            if (!declaration.Nodes.Any(
                node => node is GroupNode target
                    && target.Id == currentGroup.Id
                    && target.Kind == currentGroup.Kind))
            {
                return false;
            }

            runtimeByGroup.Add(currentGroup.Id, groupRuntimeItems);
        }

        var nodes = declaration.Nodes.Select(
            node =>
            {
                if (node is not GroupNode group
                    || !runtimeByGroup.TryGetValue(group.Id, out var additions))
                {
                    return node;
                }

                var ids = group.ItemIds.Concat(additions.Select(item => item.Id)).ToArray();
                var currentGroup = (GroupNode)current.Nodes[group.Id];
                // 运行时项并入后若正是当前选中项，需显式保留选中状态，
                // 否则按声明重建会把选中项重置回声明值。
                var selected = currentGroup.SelectedItemId is DockItemId selectedId
                    && additions.Any(item => item.Id == selectedId)
                    ? selectedId
                    : group.SelectedItemId;
                return new GroupNode(
                    group.Id,
                    group.Kind,
                    group.RetentionPolicy,
                    group.Visibility,
                    ids,
                    selected,
                    group.EditorState,
                    group.LogicalRegion);
            }).ToArray();
        // 工具栏顺序与自动隐藏条按「声明在前、运行时在后」合并，
        // 使对账结果与运行时状态一致且顺序可复现。
        var orders = nodes.OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Tool)
            .Select(
                group =>
                {
                    var declarationOrder = group.ItemIds
                        .Where(itemId => !runtimeIds.Contains(itemId));
                    var runtimeOrder = current.ToolBarOrders.TryGetValue(
                        group.LogicalRegion!.Value,
                        out var currentOrder)
                        ? currentOrder.ItemIds.Where(runtimeIds.Contains)
                        : group.ItemIds.Where(runtimeIds.Contains);
                    return new ToolBarOrderState(
                        group.LogicalRegion.Value,
                        declarationOrder.Concat(runtimeOrder));
                });
        var autoHideStrips = declaration.AutoHideStrips.Concat(
            current.AutoHideStrips.Values
                .Select(
                    strip => new AutoHideStripState(
                        strip.Edge,
                        strip.Entries.Where(entry => runtimeIds.Contains(entry.ItemId))))
                .Where(strip => strip.Entries.Count != 0));
        merged = new AtlasWorkspaceDefinition(
            declaration.WorkspaceId,
            declaration.RootNodeId,
            nodes,
            declaration.Items.Concat(runtimeItems),
            declaration.ToolBars,
            orders,
            autoHideStrips);
        return true;
    }

    private void RememberDeclaration(AtlasWorkspaceDefinition declaration)
    {
        previousDeclaration = declaration;
        declaredItemIds = declaration.Items.Select(item => item.Id).ToHashSet();
    }

    private AtlasHostError DefinitionError(WorkspaceId? workspaceId)
    {
        return new AtlasHostError(
            AtlasHostErrorCode.DefinitionInvalid,
            "ATLAS-DEFINITION-INVALID",
            "Atlas.Definition.Invalid",
            workspaceId);
    }

    private bool ShouldOwnWorkspace(bool declarative)
    {
        return WorkspaceOwnership
                == global::Atlas.Blazor.Components.WorkspaceOwnership.Host
            || (WorkspaceOwnership
                    == global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
                && declarative);
    }

    private void ValidateOwnership()
    {
        if (WorkspaceOwnership
            is < global::Atlas.Blazor.Components.WorkspaceOwnership.Automatic
            or > global::Atlas.Blazor.Components.WorkspaceOwnership.Host)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WorkspaceOwnership),
                WorkspaceOwnership,
                "Unknown workspace ownership.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AtlasWorkspaceHost));
        }
    }

    private static string Fingerprint(AtlasWorkspaceDefinition definition)
    {
        return string.Join(
            "|",
            new[]
            {
                definition.WorkspaceId.Value,
                definition.RootNodeId?.Value ?? string.Empty,
                string.Join(",", definition.Nodes.Select(NodeFingerprint)),
                string.Join(",", definition.Items.Select(ItemFingerprint)),
            });
    }

    private void ResetDeclarationLedger()
    {
        previousDeclaration = null;
        declaredItemIds.Clear();
        processedFingerprint = null;
        presentationFingerprint = null;
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();
    }

    private static string PresentationFingerprint(AtlasDefinitionCollector source)
    {
        return string.Join(
            "|",
            source.GetSplitPresentations()
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(
                    pair =>
                        $"{pair.Key}:{pair.Value.IsLocked}:{pair.Value.CssClass}:"
                        + $"{pair.Value.CanCollapseFirst}:{pair.Value.CanCollapseSecond}"));
    }

    private static string NodeFingerprint(LayoutNode node)
    {
        return node switch
        {
            SplitNode split => $"S:{split.Id}:{split.Axis}:{split.Basis.Kind}:{split.Basis.Value}:{split.FirstId}:{split.SecondId}",
            GroupNode group => $"G:{group.Id}:{group.Kind}:{group.RetentionPolicy}:{group.Visibility}:{group.LogicalRegion}:{group.SelectedItemId}:{string.Join("/", group.ItemIds)}",
            _ => node.Id.Value,
        };
    }

    private static string ItemFingerprint(DockItem item)
    {
        return $"I:{item.Id}:{item.Kind}:{item.Content.Kind}:{item.Content.ResourceId}:{item.Title}:{item.IconKey}:{item.Capabilities}:{item.DocumentState?.IsPinned}";
    }

}
