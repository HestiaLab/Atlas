using Atlas.Blazor.Components;

namespace Atlas.Blazor.Content;

/// <summary>
///     宿主本地内容源代际的纯构建逻辑：
///     Route/Map 的结构修订号延续与渲染器描述符合并。
/// </summary>
internal static class AtlasContentSourceCoordinator
{
    internal static IReadOnlyList<AtlasContentHostLocalRegistration> BuildGeneration(
        IReadOnlyList<AtlasContentHostLocalRegistration> previous,
        IReadOnlyList<AtlasContentRouteDescriptor> collectedRoutes,
        IReadOnlyList<AtlasContentMapEntry> stagedMap,
        ref int nextContentRevision)
    {
        // 上次提交的注册按 Kind 索引；同 Kind 同来源延续结构修订号。
        var previousByKind = previous
            .GroupBy(registration => registration.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var generation = new List<AtlasContentHostLocalRegistration>(
            collectedRoutes.Count + stagedMap.Count);
        foreach (var route in collectedRoutes)
        {
            // 相同的 Kind 与源所有者会延续结构修订号，
            // 仅值更新也能保留组件标识；
            // 结构变化（新增 Kind、移除 Kind 或所有者切换）会重新挂载内容出口。
            var revision = previousByKind.TryGetValue(route.Kind, out var previousRoute)
                && previousRoute.Source == AtlasContentRegistrationSource.Route
                ? previousRoute.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    route.Kind,
                    AtlasContentRegistrationSource.Route,
                    route.ChildContent!,
                    revision,
                    StructuralSignature: "Route"));
        }

        foreach (var entry in stagedMap)
        {
            // 映射结构签名（所有者、组件类型、有序绑定参数名）
            // 驱动修订号延续；值工厂及其结果永远不会被视为结构变化。
            var revision = previousByKind.TryGetValue(entry.Kind, out var previousEntry)
                && previousEntry.Source == AtlasContentRegistrationSource.Map
                && string.Equals(
                    previousEntry.StructuralSignature,
                    entry.StructuralSignature,
                    StringComparison.Ordinal)
                ? previousEntry.Revision
                : nextContentRevision++;
            generation.Add(
                new AtlasContentHostLocalRegistration(
                    entry.Kind,
                    AtlasContentRegistrationSource.Map,
                    entry.CreateRenderer(),
                    revision,
                    entry.StructuralSignature));
        }

        return generation;
    }
}
