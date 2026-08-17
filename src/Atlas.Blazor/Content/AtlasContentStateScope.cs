using Atlas.Core.Items;

namespace Atlas.Blazor.Content;

/// <summary>
///     解析以 <see cref="ContentReference"/> 为键的、应用拥有的业务状态。
///     同一资源的多个视图共享一个状态对象；不同的 ResourceId 解析为不同的对象。
///     状态容器归宿主所有，并在宿主释放时释放所有状态对象；
///     关闭项不会释放业务状态。
/// </summary>
public interface IAtlasContentStateScope
{
    /// <summary>
    ///     获取 <paramref name="reference"/> 的状态对象，
    ///     通过 <paramref name="factory"/> 创建，
    ///     在作用域生命周期内每个引用最多创建一次。
    /// </summary>
    TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class;

    /// <summary>
    ///     在不创建的情况下尝试获取 <paramref name="reference"/> 的状态对象。
    ///     当该引用尚未创建任何状态，或已创建的状态不是
    ///     <typeparamref name="TState"/> 类型时，返回 <see langword="false"/>。
    /// </summary>
    bool TryGetState<TState>(ContentReference reference, out TState? state)
        where TState : class;
}

/// <summary>
///     默认的宿主本地 <see cref="IAtlasContentStateScope"/>：
///     每个 <c>(Kind, ResourceId)</c> 键一个对象。
///     该作用域在 Blazor 渲染线程上使用，因此字典无需加锁。
/// </summary>
internal sealed class AtlasContentStateScope : IAtlasContentStateScope
{
    // 引用到状态对象的缓存；同一 (Kind, ResourceId) 至多创建一个状态。
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
