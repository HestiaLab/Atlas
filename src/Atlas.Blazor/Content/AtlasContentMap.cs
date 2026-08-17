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
///     不可变的宿主本地功能内容配置。
///     它是宿主配置而非运行时插件注册表：更改结构性注册需替换映射对象。
///     值工厂闭包可捕获宿主本地状态，注册为单例会让状态跨宿主共享，因此不要单例化。
/// </summary>
public sealed class AtlasContentMap
{
    // 不可变的功能映射条目；由创建期构建器快照而来。
    private readonly IReadOnlyList<AtlasContentMapEntry> entries;

    internal AtlasContentMap(IReadOnlyList<AtlasContentMapEntry> entries)
    {
        this.entries = entries;
    }

    internal IReadOnlyList<AtlasContentMapEntry> Entries => entries;

    /// <summary>
    ///     通过同步执行一次 configure 委托创建不可变内容映射。
    ///     configure 异常会向上传播，且不会返回部分映射。
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
///     <see cref="AtlasContentMap"/> 的创建期构建器。
///     实例仅提供给 <see cref="AtlasContentMap.Create"/>；
///     在 Create 返回后被捕获的调用会抛出 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class AtlasContentMapBuilder
{
    // 创建期收集的映射条目；Build 时快照为不可变数组。
    private readonly List<AtlasContentMapEntry> entries = new();

    // Create 返回后的过期标记；任何公开入口的调用都抛出。
    private bool expired;

    internal AtlasContentMapBuilder()
    {
    }

    /// <summary>
    ///     将一个序数 Kind 映射到带强类型绑定的组件；
    ///     派生自 AtlasContentComponent 的组件由渲染器自动注入 Content 参数。
    ///     保留重复的 Kind 贡献，使生效目录报告确定性冲突而非覆盖。
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
    ///     将构建器标记为过期；此后任何公开入口的调用都会抛出。
    /// </summary>
    internal void Expire()
    {
        expired = true;
    }

    /// <summary>
    ///     取回不可变内容映射快照；仅在 Create 的 configure 委托返回后调用一次。
    /// </summary>
    /// <returns>
    ///     包含已登记条目的不可变映射。
    /// </returns>
    internal AtlasContentMap Build()
    {
        // 内部方法：仅在 configure 委托返回后由 AtlasContentMap.Create 调用。
        // 过期保护公开的 MapComponent 入口；
        // 逃逸的构建器无法改动此处取得的快照。
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
///     单个映射组件的创建期类型化绑定构建器。
///     实例仅由 <see cref="AtlasContentMapBuilder.MapComponent{TComponent}"/> 提供；
///     在 Create 返回后被捕获的调用会抛出 <see cref="InvalidOperationException"/>。
/// </summary>
public sealed class AtlasComponentBinding<TComponent>
    where TComponent : IComponent
{
    // 创建期收集的参数绑定；供映射条目快照。
    private readonly List<AtlasContentBinding> bindings = new();

    // Create 返回后的过期标记；任何公开入口的调用都抛出。
    private bool expired;

    internal AtlasComponentBinding()
    {
    }

    internal IReadOnlyList<AtlasContentBinding> Bindings => bindings;

    /// <summary>
    ///     将一个可写 Blazor 参数绑定到渲染期间求值的值工厂。
    ///     选择器必须是组件上直接的公共可写 <c>[Parameter]</c> 属性，参数名由属性名派生；
    ///     同一参数重复绑定，或绑定保留的 AtlasContentComponent.Content，都会抛出。
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
    ///     将绑定构建器标记为过期；此后任何公开入口的调用都会抛出。
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
