using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>Provides an asynchronous consumer decision before an item closes.</summary>
public interface IDockItemCloseGuard
{
    /// <summary>Determines whether the requested item may close.</summary>
    ValueTask<bool> CanCloseAsync(
        DockItemCloseContext context,
        CancellationToken cancellationToken);
}

/// <summary>Provides immutable state captured for one close decision.</summary>
public sealed class DockItemCloseContext
{
    internal DockItemCloseContext(
        WorkspaceSnapshot snapshot,
        DockItem item)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>Gets the immutable snapshot captured before the guard.</summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>Gets the item requested for closure.</summary>
    public DockItem Item { get; }
}

/// <summary>Resolves an optional close guard for a workspace item.</summary>
public interface IDockItemCloseGuardResolver
{
    /// <summary>Resolves the guard for an item, or null when no guard is required.</summary>
    IDockItemCloseGuard? Resolve(DockItem item);
}
