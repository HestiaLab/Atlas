using Atlas.Core.Items;

namespace Atlas.Core;

/// <summary>
///     提供对不可变工作区快照的无锁查询。
/// </summary>
public static class AtlasWorkspaceQueries
{
    /// <summary>
    ///     按标识获取项；不存在时返回 null。
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
    ///     读取当前快照并把匹配项物化为不可变数组；
    ///     谓词只对快照求值，后续提交不影响已返回的结果。
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
