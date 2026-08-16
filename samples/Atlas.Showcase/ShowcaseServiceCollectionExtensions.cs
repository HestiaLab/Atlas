using Atlas.Blazor.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Showcase;

/// <summary>
/// Registers the Atlas services the showcase needs at startup. Content panels
/// are contributed by the workspace definition through Host-local
/// <c>AtlasContentRoute</c> declarations.
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
