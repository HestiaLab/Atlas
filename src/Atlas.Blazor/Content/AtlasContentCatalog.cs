using Atlas.Core;
using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>
/// A composition-root content registration contributed by startup DI.
/// </summary>
internal sealed record AtlasContentRegistration(string Kind, Type ComponentType);

/// <summary>Identifies which catalog source owns a renderer descriptor.</summary>
internal enum AtlasContentRegistrationSource
{
    DI,
    Route,
    Map,
}

/// <summary>Identifies the outcome of one effective Catalog lookup.</summary>
internal enum AtlasContentResolution
{
    Registered,
    Missing,
    Conflict,
}

/// <summary>
/// One renderer recipe in the effective Content Catalog. The descriptor stores a
/// renderer recipe only; it never stores a component instance or resource state.
/// A Host-local descriptor may capture callbacks, delegates, fragments, or local
/// state in its renderer closure; that capture is Host-owned runtime binding
/// state, not cataloged business data.
/// </summary>
internal sealed record AtlasContentRegistrationDescriptor(
    string Kind,
    AtlasContentRegistrationSource Source,
    Type? ComponentType,
    RenderFragment<AtlasContentContext>? Renderer,
    int Revision)
{
    internal static AtlasContentRegistrationDescriptor Di(string kind, Type componentType)
    {
        return new AtlasContentRegistrationDescriptor(
            kind,
            AtlasContentRegistrationSource.DI,
            componentType,
            null,
            Revision: 0);
    }

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
/// A Host-local renderer contribution from one committed source generation.
/// The Host owns the revision used to key outlets so structural changes remount
/// while value-only updates preserve component identity.
/// </summary>
internal sealed record AtlasContentHostLocalRegistration(
    string Kind,
    AtlasContentRegistrationSource Source,
    RenderFragment<AtlasContentContext> Renderer,
    int Revision);

/// <summary>
/// The effective per-Host Content Catalog. Startup DI registrations own the
/// composition root: duplicate ordinal Kinds fail deterministically at
/// construction, before a Host or per-Item outlet exists. Host-local Route/Map
/// contributions are combined per generation; any collision involving a
/// Host-local contribution invalidates only that Kind with a deterministic
/// conflict, and registration order never selects a winner.
/// </summary>
internal sealed class AtlasContentCatalog
{
    /// <summary>An empty catalog used when no startup DI registry is available.</summary>
    internal static readonly AtlasContentCatalog Empty = new(
        Array.Empty<AtlasContentRegistrationDescriptor>());

    private readonly IReadOnlyDictionary<string, AtlasContentRegistrationDescriptor> owners;
    private readonly IReadOnlySet<string> conflictedKinds;

    /// <summary>
    /// Builds the composition-root catalog from startup DI registrations.
    /// Duplicate ordinal Kinds throw before any Host exists.
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
    /// Produces the effective catalog for one Host by combining the composition-root
    /// DI source with one committed Host-local source generation. Host-local
    /// collisions with DI or with each other become per-Kind conflicts.
    /// </summary>
    internal AtlasContentCatalog WithHostLocal(
        IReadOnlyList<AtlasContentHostLocalRegistration> hostLocal)
    {
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
    /// Resolves one ordinal Kind once per projection. The result is shared between
    /// rendering, unavailable-state selection, and diagnostics.
    /// </summary>
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

    /// <summary>Creates a startup DI registration descriptor for one ordinal kind.</summary>
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

            if (!owners.TryAdd(descriptor.Kind, descriptor))
            {
                conflicts.Add(descriptor.Kind);
                owners.Remove(descriptor.Kind);
            }
        }

        return (owners, conflicts);
    }
}
