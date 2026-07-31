using Atlas.Core.Items;

namespace Atlas.Core;

/// <summary>Provides lock-free queries over immutable workspace snapshots.</summary>
public static class AtlasWorkspaceQueries
{
    /// <summary>Gets an item by identity, or null when it does not exist.</summary>
    public static DockItem? TryGetItem(
        this IAtlasWorkspace workspace,
        DockItemId itemId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.Current.Items.TryGetValue(itemId, out var item)
            ? item
            : null;
    }

    /// <summary>Returns an immutable materialized item query result.</summary>
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
