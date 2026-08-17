namespace Atlas.Core.Persistence;

/// <summary>
///     在不受信任的持久化输入进入领域状态前施加的资源上限：
///     文档字节数、嵌套深度、节点与项数量、字符串长度与旧版迁移步数。
/// </summary>
public sealed class LayoutPersistenceLimits
{
    /// <summary>
    ///     构造资源限制；所有上限都必须为正。
    /// </summary>
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

    /// <summary>
    ///     获取最大的 UTF-8 文档大小。
    /// </summary>
    public int MaxDocumentBytes { get; }

    /// <summary>
    ///     获取最大的 XML/布局嵌套深度。
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    ///     获取最大的布局节点数量。
    /// </summary>
    public int MaxNodeCount { get; }

    /// <summary>
    ///     获取最大的停靠项数量。
    /// </summary>
    public int MaxItemCount { get; }

    /// <summary>
    ///     获取单个组中最大的项引用数量。
    /// </summary>
    public int MaxItemsPerGroup { get; }

    /// <summary>
    ///     获取最大的持久化字符串长度。
    /// </summary>
    public int MaxStringLength { get; }

    /// <summary>
    ///     获取最大的旧版迁移步数。
    /// </summary>
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
