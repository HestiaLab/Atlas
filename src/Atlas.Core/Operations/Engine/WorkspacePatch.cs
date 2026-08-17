using Atlas.Core.Changes;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     可逆提交补丁：成对保存前后快照，支持正反向应用、校验与差异描述。
/// </summary>
internal sealed class WorkspacePatch
{
    private WorkspacePatch(WorkspaceSnapshot before, WorkspaceSnapshot after)
    {
        Before = before;
        After = after;
    }

    /// <summary>
    ///     获取补丁起点快照（应用前的状态），与 <see cref="After"/> 成对构成可逆变更单元。
    /// </summary>
    internal WorkspaceSnapshot Before { get; }

    /// <summary>
    ///     获取补丁终点快照（应用后的状态），与 <see cref="Before"/> 成对构成可逆变更单元。
    /// </summary>
    internal WorkspaceSnapshot After { get; }

    /// <summary>
    ///     基于前后快照创建补丁；两个快照必须属于同一工作区。
    /// </summary>
    /// <param name="before">补丁起点快照。</param>
    /// <param name="after">补丁终点快照。</param>
    /// <returns>
    ///     新建的补丁实例。
    /// </returns>
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

    /// <summary>
    ///     从期望的前状态正向应用补丁，并递增修订号。
    /// </summary>
    /// <param name="current">当前快照，必须与补丁起点语义一致。</param>
    /// <returns>
    ///     应用后的新快照。
    /// </returns>
    internal WorkspaceSnapshot ApplyForward(WorkspaceSnapshot current)
    {
        EnsureExpected(current, Before);
        return After.WithRevision(checked(current.Revision + 1));
    }

    /// <summary>
    ///     从期望的后状态反向应用补丁，并递增修订号。
    /// </summary>
    /// <param name="current">当前快照，必须与补丁终点语义一致。</param>
    /// <returns>
    ///     反向应用后的新快照。
    /// </returns>
    internal WorkspaceSnapshot ApplyReverse(WorkspaceSnapshot current)
    {
        EnsureExpected(current, After);
        return Before.WithRevision(checked(current.Revision + 1));
    }

    /// <summary>
    ///     比较前后快照，生成描述增删改内容的变更集。
    /// </summary>
    /// <param name="before">提交前快照。</param>
    /// <param name="after">提交后快照。</param>
    /// <param name="kind">变更集标注的操作种类。</param>
    /// <returns>
    ///     描述本次提交差异的变更集。
    /// </returns>
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
            .Where(
                key => !EqualityComparer<TValue>.Default.Equals(
                    before[key],
                    after[key])
                    && !WorkspaceSemanticComparer.ValueEquals(
                        before[key],
                        after[key]));
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
                    && WorkspaceSemanticComparer.ValueEquals(pair.Value, value));
    }
}
