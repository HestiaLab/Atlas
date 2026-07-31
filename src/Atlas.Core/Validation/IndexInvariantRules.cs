namespace Atlas.Core.Validation;

internal static class IndexInvariantRules
{
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
                    "A node index key must be non-default and equal the node identity.");
            }
        }

        foreach (var pair in snapshot.Items)
        {
            if (pair.Key == default || pair.Value is null || pair.Value.Id != pair.Key)
            {
                InvariantViolationSink.Add(
                    violations,
                    InvariantViolationCode.ItemKeyMismatch,
                    "An item index key must be non-default and equal the item identity.");
            }
        }
    }
}
