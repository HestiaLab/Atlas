using Atlas.Blazor.DependencyInjection;
using Atlas.Demo.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Demo;

/// <summary>
/// Registers the Atlas services the Demo needs at startup: the workspace
/// factory and the two startup-DI content kinds. The coordination panels
/// (product actions and products table) are Host-local Route/Map contributions,
/// so they are not registered here.
/// </summary>
public static class DemoServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasDemoServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAtlasWorkspace();
        services.AddAtlasContent<AtlasDemoContent>("atlas-demo-tool");
        services.AddAtlasContent<AtlasDemoContent>("atlas-demo-editor");
        return services;
    }
}
