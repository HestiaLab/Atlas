using Atlas.Core.Items;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal static class ItemPresentationPlanner
{
    internal static WorkspacePlan Plan(
        WorkspaceSnapshot snapshot,
        UpdateItemPresentationOperation operation)
    {
        if (!snapshot.Items.TryGetValue(operation.ItemId, out var item))
        {
            return WorkspacePlan.Failure(
                new WorkspaceError(
                    WorkspaceErrorCode.ItemNotFound,
                    operation.Kind,
                    "ATLAS-ITEM-MISSING",
                    "Atlas.Item.NotFound",
                    itemId: operation.ItemId));
        }

        var title = operation.Title ?? item.Id.Value;
        if (string.Equals(item.Title, title, StringComparison.Ordinal)
            && string.Equals(item.IconKey, operation.IconKey, StringComparison.Ordinal))
        {
            return WorkspacePlan.NoChange();
        }

        var replacement = new DockItem(
            item.Id,
            item.Kind,
            item.Content,
            item.Capabilities,
            operation.Title,
            operation.IconKey,
            item.DocumentState);
        var items = snapshot.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        items[item.Id] = replacement;
        return WorkspacePlan.Commit(
            new WorkspaceSnapshot(
                snapshot.Id,
                snapshot.Revision,
                snapshot.RootNodeId,
                snapshot.Nodes,
                items,
                snapshot.ToolBars,
                snapshot.ToolBarOrders,
                snapshot.AutoHideStrips));
    }
}
