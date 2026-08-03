using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Linq.Expressions;
using System.Reflection;

namespace Atlas.Blazor.Content;

/// <summary>One strongly typed parameter binding in a functional content map.</summary>
internal sealed record AtlasContentBinding(
    string ParameterName,
    Func<AtlasContentContext, object?> ValueFactory);

/// <summary>
/// One functional map contribution for a Kind. The entry stores a renderer recipe
/// only; value factories are evaluated inside the outlet's guarded projection and
/// may capture Host-local application state.
/// </summary>
internal sealed record AtlasContentMapEntry(
    string Kind,
    Type ComponentType,
    bool InjectsContentContext,
    IReadOnlyList<AtlasContentBinding> Bindings)
{
    /// <summary>
    /// Stable structural metadata used for renderer-revision carry-forward. It
    /// contains the owner, component type, and ordered bound parameter names;
    /// current values and factory results are never structural.
    /// </summary>
    internal string StructuralSignature =>
        $"Map:{ComponentType.FullName}:{string.Join(",", Bindings.Select(binding => binding.ParameterName))}";

    internal RenderFragment<AtlasContentContext> CreateRenderer()
    {
        return context => builder =>
        {
            var parameters = new Dictionary<string, object?>();
            foreach (var binding in Bindings)
            {
                parameters[binding.ParameterName] = binding.ValueFactory(context);
            }

            if (InjectsContentContext)
            {
                parameters[nameof(AtlasContentComponent.Content)] = context;
            }

            builder.OpenComponent<DynamicComponent>(0);
            builder.AddAttribute(1, "Type", ComponentType);
            builder.AddAttribute(2, "Parameters", parameters);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Immutable Host-local functional content configuration. It is host
/// configuration, not a runtime plugin registry: changing structural
/// registrations requires replacing the map object. Do not register the map as a
/// singleton service.
/// </summary>
public sealed class AtlasContentMap
{
    private readonly IReadOnlyList<AtlasContentMapEntry> entries;

    internal AtlasContentMap(IReadOnlyList<AtlasContentMapEntry> entries)
    {
        this.entries = entries;
    }

    internal IReadOnlyList<AtlasContentMapEntry> Entries => entries;

    /// <summary>
    /// Creates an immutable content map by executing the configure delegate
    /// synchronously once. A configure exception propagates and no partial map is
    /// returned.
    /// </summary>
    public static AtlasContentMap Create(Action<AtlasContentMapBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new AtlasContentMapBuilder();
        configure(builder);
        builder.Expire();
        return builder.Build();
    }
}

/// <summary>
/// Create-time builder for <see cref="AtlasContentMap"/>. Instances are supplied
/// only to <see cref="AtlasContentMap.Create"/>; captured calls after Create
/// returns throw <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class AtlasContentMapBuilder
{
    private readonly List<AtlasContentMapEntry> entries = new();
    private bool expired;

    internal AtlasContentMapBuilder()
    {
    }

    /// <summary>
    /// Maps one ordinal Kind to a component with strongly typed bindings. Duplicate
    /// Kind contributions are retained so the effective Catalog reports a
    /// deterministic conflict rather than overwriting.
    /// </summary>
    public AtlasContentMapBuilder MapComponent<TComponent>(
        string kind,
        Action<AtlasComponentBinding<TComponent>> configure)
        where TComponent : IComponent
    {
        ThrowIfExpired();
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("A content kind cannot be empty.", nameof(kind));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var binding = new AtlasComponentBinding<TComponent>();
        configure(binding);
        binding.Expire();

        entries.Add(
            new AtlasContentMapEntry(
                kind,
                typeof(TComponent),
                typeof(AtlasContentComponent).IsAssignableFrom(typeof(TComponent)),
                binding.Bindings));
        return this;
    }

    internal void Expire()
    {
        expired = true;
    }

    internal AtlasContentMap Build()
    {
        // Internal: called only by AtlasContentMap.Create after the configure
        // delegate returns. Expiration guards the public MapComponent entry point;
        // an escaped builder cannot mutate the snapshot taken here.
        return new AtlasContentMap(entries.ToArray());
    }

    private void ThrowIfExpired()
    {
        if (expired)
        {
            throw new InvalidOperationException(
                "An AtlasContentMapBuilder is only valid inside AtlasContentMap.Create.");
        }
    }
}

/// <summary>
/// Create-time typed binding builder for one mapped component. Instances are
/// supplied only by <see cref="AtlasContentMapBuilder.MapComponent{TComponent}"/>;
/// captured calls after Create returns throw <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class AtlasComponentBinding<TComponent>
    where TComponent : IComponent
{
    private readonly List<AtlasContentBinding> bindings = new();
    private bool expired;

    internal AtlasComponentBinding()
    {
    }

    internal IReadOnlyList<AtlasContentBinding> Bindings => bindings;

    /// <summary>
    /// Binds one writable Blazor parameter to a value factory evaluated during
    /// render. The selector must be a direct public writable
    /// <c>[Parameter]</c> property; parameter names are derived internally.
    /// </summary>
    public AtlasComponentBinding<TComponent> Bind<TValue>(
        Expression<Func<TComponent, TValue>> parameter,
        Func<AtlasContentContext, TValue> valueFactory)
    {
        ThrowIfExpired();
        if (parameter is null)
        {
            throw new ArgumentNullException(nameof(parameter));
        }

        if (valueFactory is null)
        {
            throw new ArgumentNullException(nameof(valueFactory));
        }

        var property = ValidateSelector<TValue>(parameter);
        if (bindings.Any(binding => binding.ParameterName == property.Name))
        {
            throw new ArgumentException(
                $"The content parameter '{property.Name}' is bound more than once.",
                nameof(parameter));
        }

        bindings.Add(
            new AtlasContentBinding(
                property.Name,
                context => (object?)valueFactory(context)));
        return this;
    }

    internal void Expire()
    {
        expired = true;
    }

    private static PropertyInfo ValidateSelector<TValue>(
        Expression<Func<TComponent, TValue>> selector)
    {
        if (selector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property
            || member.Expression is not ParameterExpression parameter
            || !ReferenceEquals(parameter, selector.Parameters[0]))
        {
            throw new ArgumentException(
                "A content parameter selector must be a direct property access on the component.",
                nameof(selector));
        }

        if (property.PropertyType != typeof(TValue))
        {
            throw new ArgumentException(
                $"The content parameter '{property.Name}' must be bound with its exact type.",
                nameof(selector));
        }

        if (!property.CanWrite || property.GetSetMethod()?.IsPublic != true)
        {
            throw new ArgumentException(
                $"The content parameter '{property.Name}' must be a public writable property.",
                nameof(selector));
        }

        if (!property.IsDefined(typeof(ParameterAttribute), inherit: true))
        {
            throw new ArgumentException(
                $"The member '{property.Name}' is not a Blazor component parameter.",
                nameof(selector));
        }

        if (property.IsDefined(typeof(CascadingParameterAttribute), inherit: true))
        {
            throw new ArgumentException(
                $"The member '{property.Name}' is a cascading parameter and cannot be bound.",
                nameof(selector));
        }

        if (typeof(AtlasContentComponent).IsAssignableFrom(typeof(TComponent))
            && property.Name == nameof(AtlasContentComponent.Content))
        {
            throw new ArgumentException(
                "The reserved AtlasContentComponent.Content parameter is supplied automatically and cannot be bound.",
                nameof(selector));
        }

        return property;
    }

    private void ThrowIfExpired()
    {
        if (expired)
        {
            throw new InvalidOperationException(
                "An AtlasComponentBinding is only valid inside AtlasContentMap.Create.");
        }
    }
}
