using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;

namespace Atlas.Blazor.Interop;

/// <summary>
///     把已通过 Host 级守卫的拖拽消息映射为 Core 操作；
///     只做 source/target/zone/index 语义转换，不执行操作、不持有会话状态。
/// </summary>
internal static class AtlasDockIntentFactory
{
    internal static LayoutOperation? Create(
        AtlasInteractionMessage message,
        WorkspaceSnapshot snapshot,
        DockItemId itemId,
        LayoutNodeId targetGroupId,
        DockItem item,
        GroupNode target)
    {
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
                return null;
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
                        return null;
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
                    return null;
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
                return null;
            }

            if (snapshot.ToolBarOrders.TryGetValue(targetRegion, out var targetOrder)
                && targetOrder.ItemIds.Contains(itemId))
            {
                if (message.TargetKind is not (
                    "toolbar-insertion" or "toolbar-region" or "drop-scaffold"))
                {
                    return null;
                }

                var wireIndex = message.InsertionIndex ?? targetOrder.ItemIds.Count;
                if (wireIndex > targetOrder.ItemIds.Count)
                {
                    return null;
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
                return null;
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
                    return null;
                }

                var targetItemId = new DockItemId(message.TargetItemId);
                var targetItemIndex = target.ItemIds.ToList().IndexOf(targetItemId);
                var targetToolBarIndex = targetOrder.ItemIds.ToList().IndexOf(targetItemId);
                if (targetItemIndex < 0
                    || targetToolBarIndex < 0
                    || toolbarWireIndex < targetToolBarIndex
                    || toolbarWireIndex > targetToolBarIndex + 1)
                {
                    return null;
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

        return operation;
    }
}
