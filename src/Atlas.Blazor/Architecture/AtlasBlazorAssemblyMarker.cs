using Atlas.Core.Architecture;

namespace Atlas.Blazor.Architecture;

/// <summary>
///     标记 Atlas.Blazor 程序集，供架构检查与反射扫描定位程序集边界。
/// </summary>
internal static class AtlasBlazorAssemblyMarker
{
    /// <summary>
    ///     获取 Atlas.Core 程序集的标记类型，用于跨程序集定位核心标记。
    /// </summary>
    internal static Type CoreMarker => typeof(AtlasCoreAssemblyMarker);
}
