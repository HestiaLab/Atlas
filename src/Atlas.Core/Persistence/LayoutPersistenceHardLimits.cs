namespace Atlas.Core.Persistence;

/// <summary>
///     Absolute ceilings fixed by the embedded persistence XSD. Configured
///     <see cref="LayoutPersistenceLimits"/> values may only lower these caps.
/// </summary>
internal static class LayoutPersistenceHardLimits
{
    internal const int MaxItemCount = 4096;
    internal const int MaxItemsPerGroup = 1024;
    internal const int MaxStringLength = 4096;
}
