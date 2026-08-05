using Atlas.Core.Items;

namespace Atlas.Blazor.Content;

/// <summary>
/// Resolves application-owned business state keyed by
/// <see cref="ContentReference"/>. Multiple views of the same resource share
/// one state object; distinct ResourceIds resolve distinct objects. The state
/// container is owned by the Host and releases all state objects on Host
/// disposal; item close does not release business state.
/// </summary>
public interface IAtlasContentStateScope
{
    /// <summary>
    /// Gets the state object for <paramref name="reference"/>, creating it
    /// through <paramref name="factory"/> at most once per reference for the
    /// lifetime of the scope.
    /// </summary>
    TState GetOrCreate<TState>(ContentReference reference, Func<TState> factory)
        where TState : class;

    /// <summary>
    /// Tries to get the state object for <paramref name="reference"/> without
    /// creating it. Returns <see langword="false"/> when no state was created
    /// for the reference or the created state is not of type
    /// <typeparamref name="TState"/>.
    /// </summary>
    bool TryGetState<TState>(ContentReference reference, out TState? state)
        where TState : class;
}

/// <summary>
/// Default Host-local <see cref="IAtlasContentStateScope"/>: one object per
/// <c>(Kind, ResourceId)</c> key. The scope is used on the Blazor rendering
/// thread, so the dictionary needs no locking.
/// </summary>
internal sealed class AtlasContentStateScope : IAtlasContentStateScope
{
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
