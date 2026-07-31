namespace Atlas.Core.Persistence;

/// <summary>Bounds untrusted persistence input before it becomes domain state.</summary>
public sealed class LayoutPersistenceLimits
{
    /// <summary>Initializes persistence resource limits.</summary>
    public LayoutPersistenceLimits(
        int maxDocumentBytes = 1024 * 1024,
        int maxDepth = 64,
        int maxNodeCount = 4096,
        int maxItemCount = 4096,
        int maxItemsPerGroup = 1024,
        int maxStringLength = 4096,
        int maxMigrationSteps = 1)
    {
        MaxDocumentBytes = RequirePositive(maxDocumentBytes, nameof(maxDocumentBytes));
        MaxDepth = RequirePositive(maxDepth, nameof(maxDepth));
        MaxNodeCount = RequirePositive(maxNodeCount, nameof(maxNodeCount));
        MaxItemCount = RequirePositive(maxItemCount, nameof(maxItemCount));
        MaxItemsPerGroup = RequirePositive(maxItemsPerGroup, nameof(maxItemsPerGroup));
        MaxStringLength = RequirePositive(maxStringLength, nameof(maxStringLength));
        MaxMigrationSteps = RequirePositive(maxMigrationSteps, nameof(maxMigrationSteps));
    }

    /// <summary>Gets the maximum UTF-8 document size.</summary>
    public int MaxDocumentBytes { get; }

    /// <summary>Gets the maximum XML/layout nesting depth.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the maximum layout node count.</summary>
    public int MaxNodeCount { get; }

    /// <summary>Gets the maximum dock item count.</summary>
    public int MaxItemCount { get; }

    /// <summary>Gets the maximum item references in one group.</summary>
    public int MaxItemsPerGroup { get; }

    /// <summary>Gets the maximum persisted string length.</summary>
    public int MaxStringLength { get; }

    /// <summary>Gets the maximum legacy migration steps.</summary>
    public int MaxMigrationSteps { get; }

    private static int RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A limit must be positive.");
        }

        return value;
    }
}
