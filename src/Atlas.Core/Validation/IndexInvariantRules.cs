namespace Atlas.Core.Validation;

/// <summary>
///     校验快照索引字典与条目身份的一致性。
/// </summary>
internal static class IndexInvariantRules
{
    /// <summary>
    ///     检查节点与项索引的键值一致性：键必须非默认且等于条目自身的标识。
    /// </summary>
    /// <param name="snapshot">待校验的工作区快照。</param>
    /// <param name="violations">收集违规项的目标集合。</param>
    internal static void Validate(
        WorkspaceSnapshot snapshot,
        ICollection<InvariantViolation> violations)
    {
        foreach (var pair in snapshot.Nodes)
        {
            if (pair.Key == default || pair.Value is null || pair.Value.Id != pair.Key)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.NodeKeyMismatch,
                    "A node index key must be non-default and equal the node identity.",
                    nodeId: pair.Key);
            }
        }

        foreach (var pair in snapshot.Items)
        {
            if (pair.Key == default || pair.Value is null || pair.Value.Id != pair.Key)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ItemKeyMismatch,
                    "An item index key must be non-default and equal the item identity.",
                    itemId: pair.Key);
            }
        }
    }
}
