using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Operations.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Blazor.DependencyInjection;

/// <summary>Registers Atlas Blazor projection services.</summary>
public static class AtlasServiceCollectionExtensions
{
    /// <summary>Registers the stateless workspace factory and scoped content registry.</summary>
    public static IServiceCollection AddAtlasWorkspace(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAtlasWorkspaceFactory, AtlasWorkspaceFactory>();
        services.TryAddScoped(
            provider => new AtlasContentCatalog(
                provider.GetServices<AtlasContentRegistration>()));
        return services;
    }

    /// <summary>Registers a component provider for one ordinal content kind.</summary>
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
