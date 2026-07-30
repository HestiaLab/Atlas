using Atlas.Core.Items;

namespace Atlas.Core.Operations.Engine;

internal interface IDockItemCloseGuard
{
    ValueTask<bool> CanCloseAsync(
        DockItemCloseContext context,
        CancellationToken cancellationToken);
}

internal sealed class DockItemCloseContext
{
    internal DockItemCloseContext(
        WorkspaceSnapshot snapshot,
        DockItem item)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    internal WorkspaceSnapshot Snapshot { get; }

    internal DockItem Item { get; }
}
