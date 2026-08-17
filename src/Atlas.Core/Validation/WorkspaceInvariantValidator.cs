namespace Atlas.Core.Validation;

/// <summary>
///     按索引、拓扑、放置三个层面校验工作区快照的不变式。
/// </summary>
internal static class WorkspaceInvariantValidator
{
    /// <summary>
    ///     执行全部不变式校验并返回发现的违规。
    /// </summary>
    /// <param name="snapshot">待校验的工作区快照。</param>
    /// <returns>
    ///     按校验顺序排列的违规列表；无违规时为空。
    /// </returns>
    internal static IReadOnlyList<InvariantViolation> Validate(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var violations = new List<InvariantViolation>();
        IndexInvariantRules.Validate(snapshot, violations);
        TopologyInvariantRules.Validate(snapshot, violations);
        PlacementInvariantRules.Validate(snapshot, violations);
        return violations.AsReadOnly();
    }
}

/// <summary>
///     提供向违规集合追加违规项的便捷入口。
/// </summary>
internal static class InvariantViolationSink
{
    /// <summary>
    ///     将一条违规追加到指定集合。
    /// </summary>
    /// <param name="violations">目标违规集合。</param>
    /// <param name="code">违规分类。</param>
    /// <param name="message">违规说明。</param>
    internal static void Add(
        ICollection<InvariantViolation> violations,
        InvariantViolationCode code,
        string message)
    {
        violations.Add(new InvariantViolation(code, message));
    }
}
