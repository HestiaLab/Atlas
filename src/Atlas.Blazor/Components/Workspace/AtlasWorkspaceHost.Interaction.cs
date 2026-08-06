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

/// <summary>Interaction members of <see cref="AtlasWorkspaceHost"/> (partial).</summary>
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

            // Equalize is a terminal commit like pointer/keyboard resize:
            // the controller reports the clamped target (0.5 clamped to the
            // split's valid range) and the basis kind, so the Host rebuilds
            // the exact basis instead of hard-coding a 50/50 proportion that
            // could violate the split's pixel constraints.
            var basis = ResolveCommittedBasis(splitId, message);
            var operation = string.Equals(
                message.Mode,
                "keyboard",
                StringComparison.Ordinal)
                ? ResizeSplitOperation.CreateKeyboard(splitId, basis)
                : new ResizeSplitOperation(splitId, basis, message.BaseRevision);

            // Queue the confirmation before executing: the Changed event fired
            // inside ExecuteAsync schedules the commit's render, which a
            // dispatcher may process while the await is in flight. OnAfterRenderAsync
            // must always see the pending split so it confirms the clear after
            // the render replaces the split's style attribute.
            pendingSplitPreviewClears.Add(message.SplitId);
            var result = await activeWorkspace.ExecuteAsync(operation);
            if (result.Status != WorkspaceResultStatus.Success)
            {
                // Rejected (stale, failure, or no-change): no render will
                // come, so clear the preview immediately to revert to the
                // committed ratio instead of freezing at the preview position.
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
