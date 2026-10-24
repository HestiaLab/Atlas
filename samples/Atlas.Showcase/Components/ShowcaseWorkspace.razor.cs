using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Layout;
using Atlas.Core.Items;
using Atlas.Core.Operations;
using Atlas.Core.Persistence;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Atlas.Showcase.Components;

public sealed partial class ShowcaseWorkspace : IAsyncDisposable
{
    private readonly AtlasXmlLayoutSerializer serializer = new();
    private IAtlasWorkspace? workspace;
    private string operationStatus = "就绪";
    private string statusTone = "ok";
    private string? persistenceEvent;
    private string? initializationError;
    private long revision;
    private bool isCompact;
    private bool disposed;

    [Inject]
    private IAtlasWorkspaceFactory Factory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Parameter]
    public string WorkspaceName { get; set; } = "atlas-showcase";

    /// <summary>
    ///     验证页每个 Host 只占半屏，强制使用紧凑布局。
    /// </summary>
    [Parameter]
    public bool ForceCompact { get; set; }

    [Parameter]
    public bool ShowVerificationLink { get; set; } = true;

    private IReadOnlyList<ShowcaseGroupDescriptor> MoveGroups => isCompact
        ? Array.Empty<ShowcaseGroupDescriptor>()
        : ShowcasePanels.Groups;

    protected override async Task OnInitializedAsync()
    {
        if (ForceCompact)
        {
            isCompact = true;
        }
        else
        {
            try
            {
                isCompact = await JS.InvokeAsync<bool>("atlasShowcaseIsCompact");
            }
            catch (JSException)
            {
                isCompact = false;
            }
        }

        await CreateWorkspaceAsync(refresh: false);
    }

    private async Task CreateWorkspaceAsync(bool refresh = true)
    {
        await DisposeWorkspaceAsync();
        initializationError = null;

        var result = Factory.Create(ShowcaseWorkspaceDefinition.Create(WorkspaceName, isCompact));
        if (!result.Succeeded || result.Value is null)
        {
            initializationError = result.Error?.DiagnosticId ?? "ATLAS-SHOWCASE-INIT-FAILED";
            workspace = null;
            revision = 0;
        }
        else
        {
            workspace = result.Value;
            workspace.Changed += OnWorkspaceChanged;
            revision = workspace.Current.Revision;
        }

        if (refresh)
        {
            await RefreshAsync();
        }
    }

    private async Task DisposeWorkspaceAsync()
    {
        if (workspace is null)
        {
            return;
        }

        var current = workspace;
        workspace = null;
        current.Changed -= OnWorkspaceChanged;
        await current.DisposeAsync();
    }

    private IReadOnlyList<ShowcasePanelView> BuildPanelViews()
    {
        if (workspace is null)
        {
            return Array.Empty<ShowcasePanelView>();
        }

        var groups = workspace.Current.Nodes.Values.OfType<GroupNode>().ToArray();
        return ShowcasePanels.CatalogOrder
            .Select(panel =>
            {
                var group = groups.FirstOrDefault(
                    candidate => candidate.ItemIds.Contains(panel.ItemId));
                return group is null
                    ? new ShowcasePanelView(
                        panel,
                        default,
                        GroupVisibility.Collapsed,
                        IsSelected: false,
                        Exists: false)
                    : new ShowcasePanelView(
                        panel,
                        group.Id,
                        group.Visibility,
                        group.SelectedItemId == panel.ItemId,
                        Exists: true);
            })
            .ToArray();
    }

    private GroupNode? FindGroup(DockItemId itemId)
    {
        return workspace?.Current.Nodes.Values
            .OfType<GroupNode>()
            .FirstOrDefault(group => group.ItemIds.Contains(itemId));
    }

    private async Task OpenPanelAsync(ShowcasePanelDescriptor panel)
    {
        if (workspace is null)
        {
            return;
        }

        var group = FindGroup(panel.ItemId);
        if (group is null)
        {
            SetStatus($"{panel.Title} 当前不存在，请重置布局", "warn");
            await RefreshAsync();
            return;
        }

        if (group.Visibility != GroupVisibility.Expanded)
        {
            var expand = await workspace.ExecuteAsync(
                new SetGroupVisibilityOperation(group.Id, GroupVisibility.Expanded));
            if (!expand.Succeeded)
            {
                SetStatus(Failure($"展开 {panel.Title} 失败", expand), "warn");
                await RefreshAsync();
                return;
            }
        }

        var result = await workspace.ExecuteAsync(new SelectItemOperation(panel.ItemId));
        SetStatus(
            result.Succeeded ? $"已打开 {panel.Title}" : Failure($"打开 {panel.Title} 失败", result),
            result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task OpenGuideAsync() => await OpenDocumentAsync(ShowcaseDocuments.Guide);

    private Task OpenCatalogAsync() => OpenPanelAsync(ShowcasePanels.Catalog);

    private Task OpenThemeAsync() => OpenPanelAsync(ShowcasePanels.Theme);

    private async Task OpenDocumentAsync(ShowcaseDocumentDescriptor document)
    {
        if (workspace is null)
        {
            return;
        }

        var group = FindGroup(document.ItemId);
        if (group is null)
        {
            SetStatus($"{document.Title} 已关闭，请使用“重置布局”恢复", "warn");
            await RefreshAsync();
            return;
        }

        if (group.Visibility != GroupVisibility.Expanded)
        {
            var expand = await workspace.ExecuteAsync(
                new SetGroupVisibilityOperation(group.Id, GroupVisibility.Expanded));
            if (!expand.Succeeded)
            {
                SetStatus(Failure($"展开 {document.Title} 失败", expand), "warn");
                await RefreshAsync();
                return;
            }
        }

        var result = await workspace.ExecuteAsync(new SelectItemOperation(document.ItemId));
        SetStatus(
            result.Succeeded ? $"已打开 {document.Title}" : Failure($"打开 {document.Title} 失败", result),
            result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task ToggleGroupAsync(ShowcasePanelView view)
    {
        if (workspace is null || !view.Exists)
        {
            return;
        }

        var target = view.GroupVisible
            ? GroupVisibility.Collapsed
            : GroupVisibility.Expanded;
        var result = await workspace.ExecuteAsync(
            new SetGroupVisibilityOperation(view.GroupId, target));
        var label = ShowcasePanels.Groups
            .FirstOrDefault(group => group.Id == view.GroupId)?.Label ?? "分组";
        SetStatus(
            result.Succeeded
                ? (target == GroupVisibility.Expanded
                    ? $"已展开{label}分组"
                    : $"已折叠{label}分组")
                : Failure("切换分组可见性失败", result),
            result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task MovePanelAsync(
        ShowcasePanelDescriptor panel,
        LayoutNodeId targetGroupId)
    {
        if (workspace is null)
        {
            return;
        }

        var group = FindGroup(panel.ItemId);
        if (group is null)
        {
            SetStatus($"{panel.Title} 当前不存在", "warn");
            await RefreshAsync();
            return;
        }

        if (group.Id == targetGroupId)
        {
            SetStatus($"{panel.Title} 已在目标区域", "ok");
            await RefreshAsync();
            return;
        }

        var result = await workspace.ExecuteAsync(
            new MoveToolOperation(panel.ItemId, targetGroupId));
        var label = ShowcasePanels.Groups
            .FirstOrDefault(target => target.Id == targetGroupId)?.Label ?? targetGroupId.Value;
        SetStatus(
            result.Succeeded
                ? $"已将 {panel.Title} 移动到{label}"
                : Failure($"移动 {panel.Title} 失败", result),
            result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task ResetWorkspaceAsync()
    {
        await CreateWorkspaceAsync(refresh: false);
        SetStatus("已重置布局", "ok");
        await RefreshAsync();
    }

    private async Task ToggleCompactAsync()
    {
        isCompact = !isCompact;
        await CreateWorkspaceAsync(refresh: false);
        SetStatus(
            isCompact
                ? "已切换到紧凑布局；布局历史已重置"
                : "已切换到标准布局；布局历史已重置",
            "ok");
        await RefreshAsync();
    }

    private async Task RetryAsync()
    {
        await CreateWorkspaceAsync(refresh: false);
        SetStatus("已重新初始化工作区", "ok");
        await RefreshAsync();
    }

    private Task OpenVerificationAsync()
    {
        Navigation.NavigateTo("verification");
        return Task.CompletedTask;
    }

    private async Task<string?> CaptureLayoutAsync()
    {
        if (workspace is null)
        {
            return null;
        }

        var xml = serializer.Serialize(workspace.Current);
        persistenceEvent = "布局已捕获为 XML";
        SetStatus("布局已捕获", "ok");
        await RefreshAsync();
        return xml;
    }

    private async Task RestoreLayoutAsync(string xml)
    {
        if (workspace is null)
        {
            return;
        }

        var options = new LayoutRestoreOptions(
            isContentRegistered: reference => ShowcaseContentKinds.All.Contains(reference.Kind),
            missingContentPolicy: MissingContentPolicy.PreservePlaceholder);
        var deserialize = serializer.DeserializeWithOptions(xml, workspace.Id, options);
        if (!deserialize.Succeeded || deserialize.Value is null)
        {
            persistenceEvent = $"反序列化失败：{deserialize.Error?.DiagnosticId ?? "未知错误"}";
            SetStatus(persistenceEvent, "warn");
            await RefreshAsync();
            return;
        }

        var result = await workspace.ExecuteAsync(
            new RestoreWorkspaceOperation(deserialize.Value));
        persistenceEvent = result.Succeeded
            ? "布局已恢复"
            : $"恢复失败：{result.Error?.DiagnosticId ?? "未知错误"}";
        SetStatus(persistenceEvent, result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task UndoAsync()
    {
        if (workspace is null)
        {
            return;
        }

        var result = await workspace.UndoAsync();
        SetStatus(result.Succeeded ? "已撤销上次更改" : "无可撤销", result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private async Task RedoAsync()
    {
        if (workspace is null)
        {
            return;
        }

        var result = await workspace.RedoAsync();
        SetStatus(result.Succeeded ? "已重做上次更改" : "无可重做", result.Succeeded ? "ok" : "warn");
        await RefreshAsync();
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        revision = args.ChangeSet.AfterRevision;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // 组件释放与最后一次工作区通知竞争时，忽略已失效的渲染请求。
        }
    }

    private void SetStatus(string message, string tone)
    {
        operationStatus = message;
        statusTone = tone;
    }

    private static string Failure(
        string action,
        WorkspaceResult<LayoutChangeSet> result)
    {
        return $"{action}：{result.Error?.DiagnosticId ?? "未知错误"}";
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await DisposeWorkspaceAsync();
    }
}
