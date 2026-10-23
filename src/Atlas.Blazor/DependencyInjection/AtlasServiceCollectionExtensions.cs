using Atlas.Blazor.Content;
using Atlas.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Blazor.DependencyInjection;

/// <summary>
///     Atlas Blazor 投影服务与内容目录的组合根注册扩展。
/// </summary>
public static class AtlasServiceCollectionExtensions
{
    /// <summary>
    ///     注册无状态工作区工厂（单例）与生效内容目录（每 DI 作用域一个）。
    ///     目录在作用域内首次解析时收集全部 AtlasContentRegistration 构建组合根，
    ///     重复 Kind 在此时抛出而非静默覆盖；TryAdd 使重复调用不产生重复注册。
    /// </summary>
    public static IServiceCollection AddAtlasWorkspace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAtlasWorkspaceFactory, AtlasWorkspaceFactory>();
        services.TryAddScoped(
            provider => new AtlasContentCatalog(
                provider.GetServices<AtlasContentRegistration>()));
        return services;
    }

    /// <summary>
    ///     为一个序数 Kind 登记组件提供程序：注册一条 AtlasContentRegistration 单例，
    ///     供 AddAtlasWorkspace 建立的作用域目录收集；
    ///     同一 Kind 重复注册会在目录构建时抛异常。
    /// </summary>
    public static IServiceCollection AddAtlasContent<TComponent>(
        this IServiceCollection services,
        string kind)
        where TComponent : AtlasContentComponent
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(AtlasContentCatalog.Create<TComponent>(kind));
        return services;
    }
}
