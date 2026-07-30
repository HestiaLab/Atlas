using Atlas.Core.Changes;

namespace Atlas.Core.Operations.Engine;

internal sealed class WorkspacePatch
{
    private WorkspacePatch(WorkspaceSnapshot before, WorkspaceSnapshot after)
    {
        Before = before;
        After = after;
    }

    internal WorkspaceSnapshot Before { get; }

    internal WorkspaceSnapshot After { get; }

    internal static WorkspacePatch Create(
        WorkspaceSnapshot before,
        WorkspaceSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.Id != after.Id)
        {
            throw new InvalidOperationException("A patch cannot change workspace identity.");
        }

        return new WorkspacePatch(before, after);
    }

    internal WorkspaceSnapshot ApplyForward(WorkspaceSnapshot current)
    {
        EnsureExpected(current, Before);
        return After.WithRevision(checked(current.Revision + 1));
    }

    internal WorkspaceSnapshot ApplyReverse(WorkspaceSnapshot current)
    {
        EnsureExpected(current, After);
        return Before.WithRevision(checked(current.Revision + 1));
    }

    internal LayoutChangeSet Describe(
        WorkspaceSnapshot before,
        WorkspaceSnapshot after,
        LayoutChangeKind kind)
    {
        return new LayoutChangeSet(
            before.Revision,
            after.Revision,
            kind,
            addedNodes: Added(before.Nodes, after.Nodes),
            removedNodes: Removed(before.Nodes, after.Nodes),
            updatedNodes: Updated(before.Nodes, after.Nodes),
            addedItems: Added(before.Items, after.Items),
            removedItems: Removed(before.Items, after.Items),
            updatedItems: Updated(before.Items, after.Items),
            updatedToolBars: ChangedKeys(before.ToolBars, after.ToolBars),
            updatedToolBarOrders: ChangedKeys(before.ToolBarOrders, after.ToolBarOrders),
            updatedAutoHideStrips: ChangedKeys(before.AutoHideStrips, after.AutoHideStrips));
    }

    private static void EnsureExpected(
        WorkspaceSnapshot current,
        WorkspaceSnapshot expected)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.Id != expected.Id
            || current.RootNodeId != expected.RootNodeId
            || !SameEntries(current.Nodes, expected.Nodes)
            || !SameEntries(current.Items, expected.Items)
            || !SameEntries(current.ToolBars, expected.ToolBars)
            || !SameEntries(current.ToolBarOrders, expected.ToolBarOrders)
            || !SameEntries(current.AutoHideStrips, expected.AutoHideStrips))
        {
            throw new InvalidOperationException(
                "The current snapshot does not match the patch expected-before state.");
        }
    }

    private static IEnumerable<TKey> Added<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> before,
        IReadOnlyDictionary<TKey, TValue> after)
        where TKey : notnull
    {
        return after.Keys.Except(before.Keys);
    }

    private static IEnumerable<TKey> Removed<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> before,
        IReadOnlyDictionary<TKey, TValue> after)
        where TKey : notnull
    {
        return before.Keys.Except(after.Keys);
    }

    private static IEnumerable<TKey> Updated<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> before,
        IReadOnlyDictionary<TKey, TValue> after)
        where TKey : notnull
    {
        return before.Keys
            .Intersect(after.Keys)
            .Where(key => !ReferenceEquals(before[key], after[key]));
    }

    private static IEnumerable<TKey> ChangedKeys<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> before,
        IReadOnlyDictionary<TKey, TValue> after)
        where TKey : notnull
    {
        return Added(before, after)
            .Concat(Removed(before, after))
            .Concat(Updated(before, after));
    }

    private static bool SameEntries<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> left,
        IReadOnlyDictionary<TKey, TValue> right)
        where TKey : notnull
    {
        return left.Count == right.Count
            && left.All(
                pair => right.TryGetValue(pair.Key, out var value)
                    && ReferenceEquals(pair.Value, value));
    }
}
