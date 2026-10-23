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
                await SetErrorAsync(DefinitionError(WorkspaceId));
                await InvokeAsync(StateHasChanged);
            }

            return;
        }

        var currentPresentationFingerprint =
            AtlasDefinitionReconciler.PresentationFingerprint(collector);
        var presentationChanged = !string.Equals(
            presentationFingerprint,
            currentPresentationFingerprint,
            StringComparison.Ordinal);
        presentationFingerprint = currentPresentationFingerprint;
        declarativeSplitterPresentations = collector.GetSplitPresentations();
        // 布局指纹未变时声明内容等价，跳过创建/对账以省去一次工作区操作；
        // 但分割条呈现变化仍需重渲染，以反映锁定与可折叠状态。
        var currentFingerprint = AtlasDefinitionReconciler.Fingerprint(declaration);
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
                await SetErrorAsync(DefinitionError(WorkspaceId));
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
        if (!AtlasDefinitionReconciler.TryMergeRuntimeItems(
                declaration,
                activeWorkspace.Current,
                previousDeclaration,
                declaredItemIds,
                out var merged))
        {
            await SetErrorAsync(DefinitionError(WorkspaceId));
            await InvokeAsync(StateHasChanged);
            return;
        }

        var reconcile = await activeWorkspace.ExecuteAsync(
            new ReconcileDefinitionOperation(
                merged!,
                activeWorkspace.Current.Revision));
        if (reconcile.Status == WorkspaceResultStatus.Failure)
        {
            await SetErrorAsync(DefinitionError(WorkspaceId));
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
        var generation = AtlasContentSourceCoordinator.BuildGeneration(
            previous,
            collectedRoutes,
            stagedMap,
            ref nextContentRevision);
        committedContentGeneration = generation;
        effectiveCatalog = (Registry ?? AtlasContentCatalog.Empty).WithHostLocal(generation);
        await InvokeAsync(StateHasChanged);
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

    private void ResetDeclarationLedger()
    {
        previousDeclaration = null;
        declaredItemIds.Clear();
        processedFingerprint = null;
        presentationFingerprint = null;
        declarativeSplitterPresentations =
            new Dictionary<LayoutNodeId, AtlasSplitterPresentation>();
    }

}
