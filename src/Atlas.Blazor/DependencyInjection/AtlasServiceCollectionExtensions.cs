using Atlas.Blazor.Content;
using Atlas.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Blazor.DependencyInjection;

/// <summary>
///     Composition-root registration extensions for Atlas Blazor projection
///     services and the content catalog.
/// </summary>
public static class AtlasServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the stateless workspace factory (singleton) and the
    ///     effective content catalog (one per DI scope). The catalog builds
    ///     its composition root from all AtlasContentRegistration instances
    ///     on first resolution within a scope; duplicate Kinds throw there
    ///     instead of being silently overwritten. TryAdd keeps repeated calls
    ///     from producing duplicate registrations.
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
    ///     Registers a component provider for one ordinal Kind: adds a single
    ///     AtlasContentRegistration collected by the scoped catalog built by
    ///     AddAtlasWorkspace. Registering the same Kind twice throws when the
    ///     catalog is built.
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
