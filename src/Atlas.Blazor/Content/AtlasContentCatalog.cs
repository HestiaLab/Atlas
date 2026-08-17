using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>
///     启动 DI 贡献的组合根内容注册：Kind 到组件类型的绑定。
///     重复 Kind 在目录构造时直接抛异常，不做静默覆盖。
/// </summary>
internal sealed record AtlasContentRegistration(string Kind, Type ComponentType);

/// <summary>
///     标识哪个目录源拥有渲染器描述符。
/// </summary>
internal enum AtlasContentRegistrationSource
{
    DI,
    Route,
    Map,
}

/// <summary>
///     标识一次生效目录查找的结果。
/// </summary>
internal enum AtlasContentResolution
{
    Registered,
    Missing,
    Conflict,
}

/// <summary>
///     生效目录中的一条渲染器配方，由 DI 或宿主本地来源产生。
///     只存配方不存实例：DI 来源按组件类型动态实例化，
///     宿主本地来源携带渲染器闭包（可捕获宿主私有状态）。
/// </summary>
internal sealed record AtlasContentRegistrationDescriptor(
    string Kind,
    AtlasContentRegistrationSource Source,
    Type? ComponentType,
    RenderFragment<AtlasContentContext>? Renderer,
    int Revision)
{
    /// <summary>
    ///     构造 DI 来源的渲染器配方。
    ///     不携带渲染器委托，出口按组件类型实例化；修订恒为 0（DI 来源无结构演进）。
    /// </summary>
    /// <param name="kind">内容种类。</param>
    /// <param name="componentType">渲染内容用的组件类型。</param>
    internal static AtlasContentRegistrationDescriptor Di(string kind, Type componentType)
    {
        return new AtlasContentRegistrationDescriptor(
            kind,
            AtlasContentRegistrationSource.DI,
            componentType,
            null,
            Revision: 0);
    }

    /// <summary>
    ///     构造宿主本地 Route/Map 来源的渲染器配方。
    ///     携带宿主渲染器闭包与修订号：修订由出口作 SetKey 定键，
    ///     结构变化递增使组件重挂载，仅值变化保持组件标识。
    /// </summary>
    /// <param name="kind">内容种类。</param>
    /// <param name="source">宿主本地来源：Route 或 Map。</param>
    /// <param name="renderer">为出口渲染内容的宿主本地渲染器委托。</param>
    /// <param name="revision">结构性变更时递增的修订号，供出口定键。</param>
    internal static AtlasContentRegistrationDescriptor HostLocal(
        string kind,
        AtlasContentRegistrationSource source,
        RenderFragment<AtlasContentContext> renderer,
        int revision)
    {
        return new AtlasContentRegistrationDescriptor(
            kind,
            source,
            null,
            renderer,
            revision);
    }
}

/// <summary>
///     一次已提交源生成中的宿主本地渲染器贡献。
///     Revision 供出口 SetKey 定键：结构变化递增使组件重挂载，仅值变化保持标识；
///     StructuralSignature 只含 Kind/源元数据，不含渲染器委托或当前值，
///     保证签名跨渲染稳定可比（用于判断结构是否变化）。
/// </summary>
internal sealed record AtlasContentHostLocalRegistration(
    string Kind,
    AtlasContentRegistrationSource Source,
    RenderFragment<AtlasContentContext> Renderer,
    int Revision,
    string StructuralSignature);

/// <summary>
///     把内容 Kind 解析为渲染器配方的注册目录。
///     DI 组合根在构造时校验重复 Kind（抛异常）；
///     宿主本地贡献经 WithHostLocal 合并，与 DI 或彼此冲突的 Kind
///     标记为 Conflict——解析顺序不决定赢家，冲突显式暴露而非静默覆盖。
/// </summary>
internal sealed class AtlasContentCatalog
{
    /// <summary>
    ///     无可用启动 DI 注册表时的空目录；所有 Kind 解析为 Missing。
    /// </summary>
    internal static readonly AtlasContentCatalog Empty = new(
        Array.Empty<AtlasContentRegistrationDescriptor>());

    // Kind 到渲染器配方的生效映射；冲突的 Kind 不在此列。
    private readonly IReadOnlyDictionary<string, AtlasContentRegistrationDescriptor> owners;

    // 多来源冲突的 Kind 集合；TryResolve 对其确定性返回 Conflict。
    private readonly IReadOnlySet<string> conflictedKinds;

    /// <summary>
    ///     从启动 DI 注册构建组合根目录；重复 Kind 在宿主存在前即抛出。
    /// </summary>
    internal AtlasContentCatalog(IEnumerable<AtlasContentRegistration> registrations)
    {
        var descriptors = new List<AtlasContentRegistrationDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (!seen.Add(registration.Kind))
            {
                throw new InvalidOperationException(
                    $"Atlas content kind '{registration.Kind}' is registered more than once.");
            }

            descriptors.Add(
                AtlasContentRegistrationDescriptor.Di(
                    registration.Kind,
                    registration.ComponentType));
        }

        (owners, conflictedKinds) = Build(descriptors);
    }

    private AtlasContentCatalog(IReadOnlyList<AtlasContentRegistrationDescriptor> descriptors)
    {
        (owners, conflictedKinds) = Build(descriptors);
    }

    /// <summary>
    ///     为单个宿主生成生效目录：把 DI 组合根与一次已提交的宿主本地贡献合并。
    ///     无宿主本地贡献时直接返回自身（复用目录实例，避免无谓重建）；
    ///     宿主本地与 DI 或彼此冲突的 Kind 标记为 Conflict。
    /// </summary>
    /// <param name="hostLocal">本次已提交的宿主本地渲染器贡献集合。</param>
    internal AtlasContentCatalog WithHostLocal(
        IReadOnlyList<AtlasContentHostLocalRegistration> hostLocal)
    {
        // 没有宿主本地贡献时合并结果与当前目录完全相同；直接返回自身以复用目录实例，避免无谓重建。
        if (hostLocal.Count == 0)
        {
            return this;
        }

        var descriptors = new List<AtlasContentRegistrationDescriptor>(owners.Values);
        foreach (var registration in hostLocal)
        {
            descriptors.Add(
                AtlasContentRegistrationDescriptor.HostLocal(
                    registration.Kind,
                    registration.Source,
                    registration.Renderer,
                    registration.Revision));
        }

        return new AtlasContentCatalog(descriptors);
    }

    /// <summary>
    ///     解析 Kind 为渲染器配方，返回 Registered/Conflict/Missing 三态。
    ///     同一判定在渲染、占位选择与诊断间共享，保证各处结果一致。
    /// </summary>
    /// <param name="kind">要解析的内容种类。</param>
    /// <param name="descriptor">Registered 时为渲染器配方；否则为 <see langword="null"/>。</param>
    internal AtlasContentResolution TryResolve(
        string kind,
        out AtlasContentRegistrationDescriptor? descriptor)
    {
        if (conflictedKinds.Contains(kind))
        {
            descriptor = null;
            return AtlasContentResolution.Conflict;
        }

        if (owners.TryGetValue(kind, out descriptor))
        {
            return AtlasContentResolution.Registered;
        }

        descriptor = null;
        return AtlasContentResolution.Missing;
    }

    /// <summary>
    ///     为一个序数 kind 创建启动 DI 注册描述符。
    /// </summary>
    internal static AtlasContentRegistration Create<TComponent>(string kind)
        where TComponent : AtlasContentComponent
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("A content kind cannot be empty.", nameof(kind));
        }

        return new AtlasContentRegistration(kind, typeof(TComponent));
    }

    private static (
        IReadOnlyDictionary<string, AtlasContentRegistrationDescriptor> owners,
        IReadOnlySet<string> conflictedKinds) Build(
            IReadOnlyList<AtlasContentRegistrationDescriptor> descriptors)
    {
        var owners = new Dictionary<string, AtlasContentRegistrationDescriptor>(
            StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Kind))
            {
                throw new ArgumentException("A content kind cannot be empty.", nameof(descriptors));
            }

            // 冲突的 Kind 连首个注册也不保留：同时移出 owners 并记入冲突集，
            // 使 TryResolve 确定性返回 Conflict，而不是由注册顺序决定赢家。
            if (!owners.TryAdd(descriptor.Kind, descriptor))
            {
                conflicts.Add(descriptor.Kind);
                owners.Remove(descriptor.Kind);
            }
        }

        return (owners, conflicts);
    }
}
