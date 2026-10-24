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
    ///     注册无状态工作区工厂（单例）与有效内容目录（每个 DI 作用域一个）。
    ///     目录在作用域内首次解析时，从所有 AtlasContentRegistration 实例构建
    ///     其组合根；重复 Kind 会在那里抛出异常，而不是被静默覆盖。TryAdd
    ///     避免重复调用产生重复注册。
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
    ///     为一个序数 Kind 注册组件提供程序：添加单个 AtlasContentRegistration，
    ///     由 AddAtlasWorkspace 构建的作用域目录收集。同一 Kind 注册两次时，
    ///     在目录构建时抛出异常。
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
