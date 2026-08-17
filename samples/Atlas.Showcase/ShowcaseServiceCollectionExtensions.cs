using Atlas.Blazor.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Showcase;

/// <summary>
///     在启动时注册示例应用所需的 Atlas 服务。
///     内容面板由工作区定义通过宿主本地的 <c>AtlasContentRoute</c> 声明提供。
/// </summary>
public static class ShowcaseServiceCollectionExtensions
{
    public static IServiceCollection AddShowcaseServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAtlasWorkspace();
        return services;
    }
}
