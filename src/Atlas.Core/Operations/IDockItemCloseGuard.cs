using Atlas.Core.Items;

namespace Atlas.Core.Operations;

/// <summary>
///     Asks the consumer for an asynchronous release decision before an item closes;
///     the close is cancelled when the guard returns false.
/// </summary>
public interface IDockItemCloseGuard
{
    /// <summary>
    ///     Determines whether the requested item can be closed.
    /// </summary>
    ValueTask<bool> CanCloseAsync(
        DockItemCloseContext context,
        CancellationToken cancellationToken);
}

/// <summary>
///     Captures the immutable context required for a single close decision:
///     the workspace snapshot at guard evaluation time and the target item.
/// </summary>
public sealed class DockItemCloseContext
{
    internal DockItemCloseContext(
        WorkspaceSnapshot snapshot,
        DockItem item)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    ///     Gets the immutable snapshot captured before the guard ran.
    /// </summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     Gets the item requested for closing.
    /// </summary>
    public DockItem Item { get; }
}

/// <summary>
///     Resolves an optional close guard per item; items without a guard are closed directly.
/// </summary>
public interface IDockItemCloseGuardResolver
{
    /// <summary>
    ///     Resolves the guard for an item; null when no guard is required.
    /// </summary>
    IDockItemCloseGuard? Resolve(DockItem item);
}
