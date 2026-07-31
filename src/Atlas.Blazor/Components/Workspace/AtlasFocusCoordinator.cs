using Atlas.Core;

namespace Atlas.Blazor.Components;

internal sealed class AtlasFocusCoordinator
{
    private readonly object gate = new();
    private DockItemId? itemId;
    private long revision;
    private long version;

    internal long Version
    {
        get
        {
            lock (gate)
            {
                return version;
            }
        }
    }

    internal void Request(DockItemId requestedItemId, long afterRevision)
    {
        lock (gate)
        {
            itemId = requestedItemId;
            revision = afterRevision;
            version++;
        }
    }

    internal bool TryClaim(DockItemId candidate, long renderedRevision)
    {
        lock (gate)
        {
            if (itemId != candidate || renderedRevision < revision)
            {
                return false;
            }

            itemId = null;
            version++;
            return true;
        }
    }

    internal void Clear()
    {
        lock (gate)
        {
            itemId = null;
            version++;
        }
    }
}
