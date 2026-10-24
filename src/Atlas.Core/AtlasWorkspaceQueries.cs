using Atlas.Core.Items;

namespace Atlas.Core;

/// <summary>
///     在不可变工作区快照上提供无锁查询。
/// </summary>
public static class AtlasWorkspaceQueries
{
    /// <summary>
    ///     按标识符获取条目；条目不存在时返回 null。
    /// </summary>
    public static DockItem? TryGetItem(
        this IAtlasWorkspace workspace,
        DockItemId itemId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.Current.Items.TryGetValue(itemId, out var item)
            ? item
            : null;
    }

    /// <summary>
    ///     读取当前快照，把匹配条目物化为不可变数组；谓词只针对该快照求值，
    ///     因此后续提交不会影响返回结果。
    /// </summary>
    public static IReadOnlyList<DockItem> EnumerateItems(
        this IAtlasWorkspace workspace,
        Func<DockItem, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var snapshot = workspace.Current;
        var items = predicate is null
            ? snapshot.Items.Values
            : snapshot.Items.Values.Where(predicate);
        return Array.AsReadOnly(items.ToArray());
    }
}
