using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Linq.Expressions;
using System.Reflection;

namespace Atlas.Blazor.Content;

/// <summary>
///     功能内容映射中的一条强类型参数绑定：
///     参数名取自绑定选择器指向的属性名，值工厂在渲染时刻求值。
/// </summary>
internal sealed record AtlasContentBinding(
    string ParameterName,
    Func<AtlasContentContext, object?> ValueFactory);

/// <summary>
///     一个 Kind 的一条功能映射贡献。
///     条目仅存储渲染器配方：组件类型、参数绑定与是否注入 Content 上下文；
///     值工厂在出口的受保护投影内求值，可捕获宿主本地的应用程序状态。
/// </summary>
internal sealed record AtlasContentMapEntry(
    string Kind,
    Type ComponentType,
    bool InjectsContentContext,
    IReadOnlyList<AtlasContentBinding> Bindings)
{
    /// <summary>
    ///     驱动渲染器修订顺延的稳定结构元数据。
    ///     由来源标记 Map、组件类型全名与有序绑定参数名拼接；
    ///     值工厂及其当前结果不属于结构，仅值变化不改变签名。
    /// </summary>
    internal string StructuralSignature =>
        $"Map:{ComponentType.FullName}:{string.Join(",", Bindings.Select(binding => binding.ParameterName))}";

    /// <summary>
    ///     创建供出口渲染的渲染器委托：每次渲染新建参数字典并在渲染时刻求值值工厂，
    ///     使绑定始终反映最新的宿主状态。
    /// </summary>
    /// <returns>
    ///     渲染匹配 Kind 组件的 RenderFragment。
    /// </returns>
    internal RenderFragment<AtlasContentContext> CreateRenderer()
    {
        // 渲染器委托创建一次并在多次渲染间复用，但每次渲染都新建参数字典、
        // 在渲染时刻求值值工厂，使绑定始终反映最新的宿主状态。
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
///     Immutable Host-local functional content configuration. It is Host
///     configuration rather than a runtime plugin registry: structural
///     registration changes require replacing the map object. Value-factory
///     closures may capture Host-local state, so the map must not be
///     registered as a singleton (that would share state across Hosts).
/// </summary>
public sealed class AtlasContentMap
{
    // Immutable functional map entries; snapshotted by the creation-time
    // builder.
    private readonly IReadOnlyList<AtlasContentMapEntry> entries;

    internal AtlasContentMap(IReadOnlyList<AtlasContentMapEntry> entries)
    {
        this.entries = entries;
    }

    internal IReadOnlyList<AtlasContentMapEntry> Entries => entries;

    /// <summary>
    ///     Creates an immutable content map by running the configure delegate
    ///     once. A configure exception propagates and no partial map is
    ///     returned.
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
///     The creation-time builder for <see cref="AtlasContentMap"/>. Instances
///     are only supplied to <see cref="AtlasContentMap.Create"/>; calls made
///     through a captured instance after Create returns throw
///     <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class AtlasContentMapBuilder
{
    // Map entries collected during creation; snapshotted to an immutable
    // array on Build.
    private readonly List<AtlasContentMapEntry> entries = new();

    // Expiry flag set after Create returns; every public entry point throws.
    private bool expired;

    internal AtlasContentMapBuilder()
    {
    }

    /// <summary>
    ///     Maps one ordinal Kind to a component with strongly typed bindings;
    ///     components derived from AtlasContentComponent receive the Content
    ///     parameter automatically. Duplicate Kind contributions are kept so
    ///     the effective catalog reports a deterministic conflict instead of
    ///     overwriting.
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

    /// <summary>
    ///     Marks the builder expired; every subsequent public entry point
    ///     throws.
    /// </summary>
    internal void Expire()
    {
        expired = true;
    }

    /// <summary>
    ///     Takes the immutable content map snapshot; called exactly once after
    ///     the Create configure delegate returns.
    /// </summary>
    /// <returns>
    ///     The immutable map containing the registered entries.
    /// </returns>
    internal AtlasContentMap Build()
    {
        // Internal method: only called by AtlasContentMap.Create after the
        // configure delegate returns. The expiry guard protects the public
        // MapComponent entry point; an escaped builder cannot mutate the
        // snapshot taken here.
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
///     The creation-time typed binding builder for one mapped component.
///     Instances are only supplied by
///     <see cref="AtlasContentMapBuilder.MapComponent{TComponent}"/>; calls
///     made through a captured instance after Create returns throw
///     <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class AtlasComponentBinding<TComponent>
    where TComponent : IComponent
{
    // Parameter bindings collected during creation; snapshotted by the map
    // entry.
    private readonly List<AtlasContentBinding> bindings = new();

    // Expiry flag set after Create returns; every public entry point throws.
    private bool expired;

    internal AtlasComponentBinding()
    {
    }

    internal IReadOnlyList<AtlasContentBinding> Bindings => bindings;

    /// <summary>
    ///     Binds one writable Blazor parameter to a value factory evaluated at
    ///     render time. The selector must be a direct public writable
    ///     [Parameter] property on the component and the parameter name is
    ///     derived from the property; binding the same parameter twice, or
    ///     binding the reserved AtlasContentComponent.Content, throws.
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

    /// <summary>
    ///     Marks the binding builder expired; every subsequent public entry
    ///     point throws.
    /// </summary>
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
