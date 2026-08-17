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
///     <see cref="AtlasWorkspaceHost"/> 的交互处理逻辑（partial）：
///     分割调整、键盘命令、拖拽投递与焦点协调。
/// </summary>
public sealed partial class AtlasWorkspaceHost
{
    private async Task ToggleToolAsync(
        GroupNode group,
        DockItemId itemId,
        bool isActive)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        LayoutOperation operation = isActive
            ? new SetGroupVisibilityOperation(group.Id, GroupVisibility.Collapsed)
            : new SelectItemOperation(itemId);
        await activeWorkspace.ExecuteAsync(operation);
    }

    private async Task ToggleSplitChildAsync(LayoutNodeId groupId, bool collapsed)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        await activeWorkspace.ExecuteAsync(
            new SetGroupVisibilityOperation(
                groupId,
                collapsed ? GroupVisibility.Expanded : GroupVisibility.Collapsed));
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private void OnItemActivated(object? sender, ItemActivatedEventArgs args)
    {
        if (disposed)
        {
            return;
        }

        focusCoordinator.Request(args.ItemId, args.AfterRevision);
        _ = InvokeAsync(
            () =>
            {
                if (!disposed)
                {
                    StateHasChanged();
                }
            });
    }

    private async Task OnInteractionAsync(AtlasInteractionMessage message)
    {
        if (disposed
            || activeWorkspace is null
            || message.Generation != interactionGeneration)
        {
            return;
        }

        if (string.Equals(message.Type, "resize-split", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(message.SplitId))
            {
                return;
            }

            var splitId = new LayoutNodeId(message.SplitId);
            if (!double.IsFinite(message.FirstRatio)
                || message.FirstRatio <= 0d
                || message.FirstRatio >= 1d)
            {
                return;
            }

            // Equalize 与指针/键盘调整大小一样属于最终提交：
            // 控制器报告钳制后的目标（0.5 被钳制到分割的有效范围）
            // 与基准种类，因此宿主重建精确的基准，
            // 而不是硬编码可能违反分割像素约束的 50/50 比例。
            var basis = ResolveCommittedBasis(splitId, message);
            var operation = string.Equals(
                message.Mode,
                "keyboard",
                StringComparison.Ordinal)
                ? ResizeSplitOperation.CreateKeyboard(splitId, basis)
                : new ResizeSplitOperation(splitId, basis, message.BaseRevision);

            // 在执行前将确认排队：ExecuteAsync 内部触发的 Changed 事件
            // 会调度提交的渲染，调度器可能在 await 进行中处理它。
            // OnAfterRenderAsync 必须始终看到待处理的分割，
            // 才能在渲染替换分割的 style 属性后确认清除。
            pendingSplitPreviewClears.Add(message.SplitId);
            var result = await activeWorkspace.ExecuteAsync(operation);
            if (result.Status != WorkspaceResultStatus.Success)
            {
                // 被拒绝（过期、失败或无变化）：不会再有渲染到来，
                // 因此立即清除预览，恢复到已提交的比例，
                // 而不是停留在预览位置。
                pendingSplitPreviewClears.Remove(message.SplitId);
                await NotifySplitPreviewClearedAsync(message.SplitId);
            }

            return;
        }

        if (string.Equals(message.Type, "keyboard-command", StringComparison.Ordinal)
            && message.Command is not null)
        {
            await ExecuteKeyboardCommandAsync(
                message.Command,
                message.SourceGroupId,
                message.TargetGroupId);
            return;
        }

        if (string.Equals(message.Type, "dock-item", StringComparison.Ordinal))
        {
            await ExecuteDockItemAsync(message);
        }
    }

    private async ValueTask NotifySplitPreviewClearedAsync(string splitId)
    {
        if (interactionController is not null)
        {
            await interactionController.NotifySplitPreviewClearedAsync(splitId);
        }
    }

    private SplitBasis ResolveCommittedBasis(
        LayoutNodeId splitId,
        AtlasInteractionMessage message)
    {
        if (string.Equals(
                message.BasisKind,
                "fixed-pixels",
                StringComparison.Ordinal)
            && message.PixelExtent is double pixelExtent)
        {
            return SplitBasis.FixedPixels(
                pixelExtent,
                CurrentAnchor(splitId) ?? SplitAnchor.First);
        }

        return SplitBasis.Proportional(message.FirstRatio);
    }

    private SplitAnchor? CurrentAnchor(LayoutNodeId splitId)
    {
        if (activeWorkspace is null
            || !activeWorkspace.Current.Nodes.TryGetValue(splitId, out var node)
            || node is not SplitNode split)
        {
            return null;
        }

        return split.Basis.Anchor;
    }

    private async Task ExecuteDockItemAsync(AtlasInteractionMessage message)
    {
        // JS 消息不可信且可能重放：守卫链把消息限定为本宿主、当前工作区、
        // 协议版本 1 的未消费会话，任何一项不满足都直接丢弃。
        if (activeWorkspace is null
            || message.ProtocolVersion != 1
            || !string.Equals(message.HostId, hostId, StringComparison.Ordinal)
            || !string.Equals(
                message.WorkspaceId,
                activeWorkspace.Id.Value,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(message.SessionId)
            || !message.SessionId.StartsWith($"{hostId}:", StringComparison.Ordinal)
            || consumedDragSessions.Contains(message.SessionId)
            || string.IsNullOrWhiteSpace(message.ItemId)
            || string.IsNullOrWhiteSpace(message.SourceKind)
            || string.IsNullOrWhiteSpace(message.TargetKind)
            || string.IsNullOrWhiteSpace(message.TargetGroupId)
            || message.BaseRevision is null
            || message.BaseRevision < 0)
        {
            return;
        }

        var snapshot = activeWorkspace.Current;
        var itemId = new DockItemId(message.ItemId);
        var targetGroupId = new LayoutNodeId(message.TargetGroupId);
        if (!snapshot.Items.TryGetValue(itemId, out var item)
            || !snapshot.Nodes.TryGetValue(targetGroupId, out var targetNode)
            || targetNode is not GroupNode target)
        {
            return;
        }

        LayoutOperation? operation = null;
        if (message.SourceKind == "document-tab"
            && item.Kind == DockItemKind.Document
            && target.Kind == DockItemKind.Document)
        {
            var zone = message.TargetKind switch
            {
                "document-center" => DocumentDockZone.Center,
                "document-inline-start" => DocumentDockZone.InlineStart,
                "document-inline-end" => DocumentDockZone.InlineEnd,
                "document-block-start" => DocumentDockZone.BlockStart,
                "document-block-end" => DocumentDockZone.BlockEnd,
                _ => (DocumentDockZone?)null,
            };
            if (zone is null)
            {
                return;
            }

            if (zone == DocumentDockZone.Center)
            {
                if (message.InsertionIndex is int insertionIndex)
                {
                    var sourceIndex = target.ItemIds.ToList().IndexOf(itemId);
                    if (sourceIndex < 0
                        || insertionIndex < 0
                        || insertionIndex > target.ItemIds.Count)
                    {
                        return;
                    }

                    // 同组内重排时移除源项会使后续索引前移一位，
                    // 落点索引需按源项位置修正，否则会偏移一位。
                    var targetIndex = insertionIndex > sourceIndex
                        ? insertionIndex - 1
                        : insertionIndex;
                    operation = new ReorderItemOperation(
                        itemId,
                        targetGroupId,
                        targetIndex,
                        message.BaseRevision);
                }
                else
                {
                    operation = new DockDocumentOperation(
                        itemId,
                        targetGroupId,
                        zone.Value,
                        baseRevision: message.BaseRevision);
                }
            }
            else
            {
                if (message.InsertionIndex is not null
                    || string.IsNullOrWhiteSpace(message.NewGroupId)
                    || string.IsNullOrWhiteSpace(message.NewSplitId))
                {
                    return;
                }

                operation = new DockDocumentOperation(
                    itemId,
                    targetGroupId,
                    zone.Value,
                    new LayoutNodeId(message.NewGroupId),
                    new LayoutNodeId(message.NewSplitId),
                    message.BaseRevision);
            }
        }
        else if (message.SourceKind == "toolbar-entry"
            && item.Kind == DockItemKind.Tool
            && target.Kind == DockItemKind.Tool
            && message.TargetKind is "panel-header-insertion"
                or "toolbar-insertion"
                or "toolbar-region"
                or "drop-scaffold")
        {
            if (target.LogicalRegion is not LogicalRegion targetRegion
                || message.InsertionIndex < 0
                || (message.LogicalRegion is not null
                    && !string.Equals(
                        message.LogicalRegion,
                        targetRegion.ToString(),
                        StringComparison.Ordinal)))
            {
                return;
            }

            if (snapshot.ToolBarOrders.TryGetValue(targetRegion, out var targetOrder)
                && targetOrder.ItemIds.Contains(itemId))
            {
                if (message.TargetKind is not (
                    "toolbar-insertion" or "toolbar-region" or "drop-scaffold"))
                {
                    return;
                }

                var wireIndex = message.InsertionIndex ?? targetOrder.ItemIds.Count;
                if (wireIndex > targetOrder.ItemIds.Count)
                {
                    return;
                }

                var sourceIndex = targetOrder.ItemIds.ToList().IndexOf(itemId);
                var targetIndex = wireIndex > sourceIndex
                    ? wireIndex - 1
                    : wireIndex;
                operation = new ReorderToolBarEntryOperation(
                    itemId,
                    targetRegion,
                    targetIndex,
                    message.BaseRevision);
            }
            else
            {
                operation = new MoveToolOperation(
                    itemId,
                    targetGroupId,
                    message.InsertionIndex,
                    message.BaseRevision);
            }
        }
        else if (message.SourceKind == "panel-header"
            && item.Kind == DockItemKind.Tool
            && target.Kind == DockItemKind.Tool
            && message.TargetKind is "panel-header-insertion"
                or "panel-region-center"
                or "toolbar-insertion"
                or "toolbar-region"
                or "drop-scaffold")
        {
            if (message.InsertionIndex < 0
                || (message.LogicalRegion is not null
                    && !string.Equals(
                        message.LogicalRegion,
                        target.LogicalRegion?.ToString(),
                        StringComparison.Ordinal)))
            {
                return;
            }

            var sourceIndex = target.ItemIds.ToList().IndexOf(itemId);
            if (sourceIndex >= 0)
            {
                if (message.TargetKind != "toolbar-insertion"
                    || target.LogicalRegion is not LogicalRegion targetRegion
                    || !snapshot.ToolBarOrders.TryGetValue(targetRegion, out var targetOrder)
                    || string.IsNullOrWhiteSpace(message.TargetItemId)
                    || message.InsertionIndex is not int toolbarWireIndex)
                {
                    return;
                }

                var targetItemId = new DockItemId(message.TargetItemId);
                var targetItemIndex = target.ItemIds.ToList().IndexOf(targetItemId);
                var targetToolBarIndex = targetOrder.ItemIds.ToList().IndexOf(targetItemId);
                if (targetItemIndex < 0
                    || targetToolBarIndex < 0
                    || toolbarWireIndex < targetToolBarIndex
                    || toolbarWireIndex > targetToolBarIndex + 1)
                {
                    return;
                }

                // 工具栏线索引基于工具栏顺序，而重排针对面板内顺序：
                // 先按目标项在面板中的位置换算落点，再修正移除源项的索引前移。
                var wireIndex = toolbarWireIndex == targetToolBarIndex
                    ? targetItemIndex
                    : targetItemIndex + 1;

                var targetIndex = wireIndex > sourceIndex
                    ? wireIndex - 1
                    : wireIndex;
                operation = new ReorderItemOperation(
                    itemId,
                    targetGroupId,
                    targetIndex,
                    message.BaseRevision);
            }
            else
            {
                operation = new MoveToolOperation(
                    itemId,
                    targetGroupId,
                    message.InsertionIndex,
                    message.BaseRevision);
            }
        }

        // 会话在此一次性消费：消息可能被 JS 重复投递，
        // 第二次 Add 返回 false 即被丢弃，保证一次拖拽至多提交一个操作。
        if (operation is null || !consumedDragSessions.Add(message.SessionId))
        {
            return;
        }

        await activeWorkspace.ExecuteAsync(operation);
    }

    private async Task ExecuteKeyboardCommandAsync(
        string command,
        string? sourceGroupId,
        string? targetGroupId)
    {
        if (activeWorkspace is null)
        {
            return;
        }

        // focus-* 命令只依赖目标组，不要求活动文档；
        // 必须先于活动项解析分流，否则无选中文档时会提前返回。
        if (command.StartsWith("focus-", StringComparison.Ordinal))
        {
            await FocusToolGroupAsync(targetGroupId);
            return;
        }

        var snapshot = activeWorkspace.Current;
        var documentGroups = snapshot.Nodes.Values
            .OfType<GroupNode>()
            .Where(group => group.Kind == DockItemKind.Document)
            .ToArray();
        var activeGroup = string.IsNullOrWhiteSpace(sourceGroupId)
            ? null
            : documentGroups.FirstOrDefault(
                group => group.Id == new LayoutNodeId(sourceGroupId!));
        // 消息携带的源组可能已随快照过期（组被移除或重排），
        // 退回任一有选中项的文档组作为命令目标。
        activeGroup ??= documentGroups.FirstOrDefault(
            group => group.SelectedItemId is not null);
        if (activeGroup?.SelectedItemId is not DockItemId activeItemId)
        {
            return;
        }

        LayoutOperation? operation = command switch
        {
            "next-tab" => CycleTab(activeGroup, activeItemId, 1),
            "previous-tab" => CycleTab(activeGroup, activeItemId, -1),
            "close-active-document" => new CloseItemOperation(activeItemId),
            _ => null,
        };
        if (operation is not null)
        {
            await activeWorkspace.ExecuteAsync(operation);
        }
    }

    private async Task FocusToolGroupAsync(string? targetGroupId)
    {
        if (activeWorkspace is null || string.IsNullOrWhiteSpace(targetGroupId))
        {
            return;
        }

        var targetId = new LayoutNodeId(targetGroupId);
        if (!activeWorkspace.Current.Nodes.TryGetValue(targetId, out var node)
            || node is not GroupNode target
            || target.Kind != DockItemKind.Tool
            || target.SelectedItemId is not DockItemId itemId)
        {
            return;
        }

        var result = await activeWorkspace.ExecuteAsync(
            new SelectItemOperation(itemId));
        if (result.Succeeded)
        {
            focusCoordinator.Request(itemId, activeWorkspace.Current.Revision);
            await InvokeAsync(StateHasChanged);
        }
    }

    private static SelectItemOperation? CycleTab(
        GroupNode group,
        DockItemId activeItemId,
        int direction)
    {
        if (group.ItemIds.Count < 2)
        {
            return null;
        }

        var currentIndex = group.ItemIds.ToList().IndexOf(activeItemId);
        var nextIndex = (currentIndex + direction + group.ItemIds.Count)
            % group.ItemIds.Count;
        return new SelectItemOperation(group.ItemIds[nextIndex]);
    }

    private async Task ReportErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private async Task SetErrorAsync(AtlasHostError error)
    {
        hostError = error;
        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync(error);
        }
    }

    private string InteractionConfigurationFingerprint()
    {
        var splitterOptions = SplitterOptions ?? new AtlasSplitterOptions();
        var bindings = (KeyboardCommands ?? AtlasKeyboardCommandMap.Default).Bindings;
        return string.Join(
            "|",
            interactionGeneration,
            EnableInteractions,
            splitterOptions.KeyboardStepPixels,
            splitterOptions.KeyboardCommitDelayMilliseconds,
            string.Join(
                ",",
                bindings.Select(
                    binding =>
                        $"{binding.Chord.Code}:{binding.Chord.Control}:"
                        + $"{binding.Chord.Shift}:{binding.Chord.Alt}:"
                        + $"{binding.Chord.Meta}:"
                        + $"{binding.Command}:{binding.IsGlobal}")));
    }
}
