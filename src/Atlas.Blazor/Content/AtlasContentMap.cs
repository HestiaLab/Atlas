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
///     不可变的 Host 本地函数式内容配置。它是 Host 配置，而不是运行时插件
///     注册表：结构性注册变化需要替换映射对象。值工厂闭包可能捕获 Host 本地
///     状态，因此映射不得注册为单例（否则会在多个 Host 之间共享状态）。
/// </summary>
public sealed class AtlasContentMap
{
    // 不可变的函数式映射条目；由创建期构建器快照。
    private readonly IReadOnlyList<AtlasContentMapEntry> entries;

    internal AtlasContentMap(IReadOnlyList<AtlasContentMapEntry> entries)
    {
        this.entries = entries;
    }

    internal IReadOnlyList<AtlasContentMapEntry> Entries => entries;

    /// <summary>
    ///     通过运行一次 configure 委托创建不可变内容映射。configure 异常会向外
    ///     传播，不会返回部分映射。
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
///     <see cref="AtlasContentMap"/> 的创建期构建器。实例只会提供给
///     <see cref="AtlasContentMap.Create"/>；在 Create 返回后通过捕获的实例
///     调用会抛出 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class AtlasContentMapBuilder
{
    // 创建期间收集的映射条目；Build 时快照为不可变数组。
    private readonly List<AtlasContentMapEntry> entries = new();

    // Create 返回后置位的过期标志；每个公共入口都会抛出异常。
    private bool expired;

    internal AtlasContentMapBuilder()
    {
    }

    /// <summary>
    ///     把一个序数 Kind 映射到具有强类型绑定的组件；派生自
    ///     AtlasContentComponent 的组件会自动接收 Content 参数。重复 Kind 的
    ///     贡献会保留，以便有效目录报告确定性的冲突，而不是覆盖。
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
    ///     将构建器标记为过期；此后每个公共入口都会抛出异常。
    /// </summary>
    internal void Expire()
    {
        expired = true;
    }

    /// <summary>
    ///     获取不可变内容映射快照；在 Create 的 configure 委托返回后恰好调用
    ///     一次。
    /// </summary>
    /// <returns>
    ///     包含已注册条目的不可变映射。
    /// </returns>
    internal AtlasContentMap Build()
    {
        // 内部方法：仅由 AtlasContentMap.Create 在 configure 委托返回后调用。
        // 过期守卫保护公共 MapComponent 入口；逃逸的构建器无法修改此处获取的
        // 快照。
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
///     单个已映射组件的创建期强类型绑定构建器。实例只由
///     <see cref="AtlasContentMapBuilder.MapComponent{TComponent}"/> 提供；
///     在 Create 返回后通过捕获的实例调用会抛出
///     <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class AtlasComponentBinding<TComponent>
    where TComponent : IComponent
{
    // 创建期间收集的参数绑定；由映射条目快照。
    private readonly List<AtlasContentBinding> bindings = new();

    // Create 返回后置位的过期标志；每个公共入口都会抛出异常。
    private bool expired;

    internal AtlasComponentBinding()
    {
    }

    internal IReadOnlyList<AtlasContentBinding> Bindings => bindings;

    /// <summary>
    ///     把一个可写 Blazor 参数绑定到渲染时求值的值工厂。选择器必须是组件上
    ///     直接的公共可写 [Parameter] 属性，参数名从属性派生；同一参数绑定
    ///     两次，或绑定保留的 AtlasContentComponent.Content，都会抛出异常。
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
    ///     将绑定构建器标记为过期；此后每个公共入口都会抛出异常。
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
