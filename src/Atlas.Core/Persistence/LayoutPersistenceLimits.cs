namespace Atlas.Core.Persistence;

/// <summary>
///     在不可信持久化输入进入领域状态前应用的资源上限：文档字节数、嵌套深度、
///     节点与条目数量、字符串长度以及旧版迁移步数。
/// </summary>
public sealed class LayoutPersistenceLimits
{
    /// <summary>
    ///     创建资源限制；每个上限都必须为正数。超过内嵌 schema 硬上限（4096 个
    ///     条目、每个 Group/顺序/条最多 1024 个条目引用、4096 字符标识符）的值
    ///     会被钳制到硬上限：配置只能降低、绝不能提高 schema 上限。
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
        MaxItemCount = RequirePositive(
            Math.Min(maxItemCount, LayoutPersistenceHardLimits.MaxItemCount),
            nameof(maxItemCount));
        MaxItemsPerGroup = RequirePositive(
            Math.Min(maxItemsPerGroup, LayoutPersistenceHardLimits.MaxItemsPerGroup),
            nameof(maxItemsPerGroup));
        MaxStringLength = RequirePositive(
            Math.Min(maxStringLength, LayoutPersistenceHardLimits.MaxStringLength),
            nameof(maxStringLength));
        MaxMigrationSteps = RequirePositive(maxMigrationSteps, nameof(maxMigrationSteps));
    }

    /// <summary>
    ///     获取最大 UTF-8 文档大小。
    /// </summary>
    public int MaxDocumentBytes { get; }

    /// <summary>
    ///     获取最大 XML/布局嵌套深度。
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    ///     获取最大布局节点数。
    /// </summary>
    public int MaxNodeCount { get; }

    /// <summary>
    ///     获取最大停靠条目数。
    /// </summary>
    public int MaxItemCount { get; }

    /// <summary>
    ///     获取单个 Group 内条目引用的最大数量。
    /// </summary>
    public int MaxItemsPerGroup { get; }

    /// <summary>
    ///     获取持久化字符串的最大长度。
    /// </summary>
    public int MaxStringLength { get; }

    /// <summary>
    ///     获取旧版迁移的最大步数。
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
