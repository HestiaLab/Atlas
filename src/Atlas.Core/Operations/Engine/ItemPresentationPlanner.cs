using Atlas.Core.Items;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     项呈现更新规划器：更新项的显示标题与图标标识。
/// </summary>
internal static class ItemPresentationPlanner
{
    /// <summary>
    ///     规划项呈现更新；标题与图标均未变化时不产生修订。
    /// </summary>
    /// <param name="snapshot">操作所基于的已提交快照。</param>
    /// <param name="operation">呈现更新操作。</param>
    /// <returns>
    ///     提交、无变更或失败的规划结果。
    /// </returns>
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
        var mutation = WorkspaceMutation.From(snapshot);
        mutation.ReplaceItem(replacement);
        return mutation.Commit();
    }
}
