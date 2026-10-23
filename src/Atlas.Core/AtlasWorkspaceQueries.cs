using Atlas.Core.Items;

namespace Atlas.Core;

/// <summary>
///     Provides lock-free queries over an immutable workspace snapshot.
/// </summary>
public static class AtlasWorkspaceQueries
{
    /// <summary>
    ///     Gets an item by identifier; returns null when it does not exist.
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
    ///     Reads the current snapshot and materializes matching items into an
    ///     immutable array; the predicate is evaluated only against the snapshot,
    ///     so later commits do not affect the returned result.
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
