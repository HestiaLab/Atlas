namespace Atlas.Core.Validation;

internal static class WorkspaceInvariantValidator
{
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

internal static class InvariantViolationSink
{
    internal static void Add(
        ICollection<InvariantViolation> violations,
        InvariantViolationCode code,
        string message)
    {
        violations.Add(new InvariantViolation(code, message));
    }
}
