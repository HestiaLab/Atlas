using Atlas.Core.Items;

namespace Atlas.Blazor.Content;

/// <summary>
///     解析按 <see cref="ContentReference"/> 键控的、由应用拥有的业务状态。
///     同一资源的多个视图共享一个状态对象；不同 ResourceId 解析为不同对象。
///     作用域实例由 Host 拥有。关闭条目不会释放业务状态，Atlas 也绝不会自动
///     释放状态对象：只要作用域实例可达，它们就继续存在。
/// </summary>
public interface IAtlasContentStateScope
{
    /// <summary>
    ///     获取 <paramref name="reference"/> 的状态对象；在作用域生命周期内，
    ///     每个引用最多通过 <paramref name="factory"/> 创建一次。
    /// </summary>
    TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class;

    /// <summary>
    ///     尝试获取 <paramref name="reference"/> 的状态对象而不创建它。该引用
    ///     尚未创建状态，或已创建状态不是 <typeparamref name="TState"/> 类型时
    ///     返回 <see langword="false"/>。
    /// </summary>
    bool TryGetState<TState>(ContentReference reference, out TState? state)
        where TState : class;
}

/// <summary>
///     默认的 Host 本地 <see cref="IAtlasContentStateScope"/>：每个
///     (Kind, ResourceId) 键对应一个对象。作用域在 Blazor 渲染线程上使用，
///     因此字典无需加锁。
/// </summary>
internal sealed class AtlasContentStateScope : IAtlasContentStateScope
{
    // 引用到状态的缓存；每个 (Kind, ResourceId) 键最多创建一个状态。
    private readonly Dictionary<(string Kind, string ResourceId), object> states = new();

    /// <inheritdoc />
    public TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var key = (reference.Kind, reference.ResourceId);
        if (states.TryGetValue(key, out var existing))
        {
            if (existing is not TState typed)
            {
                throw new InvalidOperationException(
                    $"Content state for '{key.Kind}'/'{key.ResourceId}' was already created as "
                    + $"{existing.GetType().FullName} and cannot be resolved as {typeof(TState).FullName}.");
            }

            return typed;
        }

        var created = factory();
        states.Add(key, created);
        return created;
    }

    /// <inheritdoc />
    public bool TryGetState<TState>(ContentReference reference, out TState? state)
        where TState : class
    {
        if (states.TryGetValue((reference.Kind, reference.ResourceId), out var existing)
            && existing is TState typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }
}
