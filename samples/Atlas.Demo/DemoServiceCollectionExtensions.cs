using Atlas.Blazor.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Demo;

/// <summary>
/// Registers the Atlas services the Demo needs at startup. Only the workspace
/// factory is registered here; every content panel (the DBC explorer, editor,
/// and properties views) is a Host-local AtlasContentRoute contribution
/// declared by AtlasDemoWorkspace.
/// </summary>
public static class DemoServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasDemoServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAtlasWorkspace();
        return services;
    }
}
