namespace Atlas.Core.Persistence;

/// <summary>
///     由内嵌持久化 XSD 固定的绝对上限。配置的
///     <see cref="LayoutPersistenceLimits"/> 值只能降低这些上限。
/// </summary>
internal static class LayoutPersistenceHardLimits
{
    internal const int MaxItemCount = 4096;
    internal const int MaxItemsPerGroup = 1024;
    internal const int MaxStringLength = 4096;
}
