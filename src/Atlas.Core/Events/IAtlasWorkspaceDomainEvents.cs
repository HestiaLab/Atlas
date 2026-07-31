namespace Atlas.Core.Events;

/// <summary>Exposes strongly typed events derived from committed workspace changes.</summary>
public interface IAtlasWorkspaceDomainEvents
{
    /// <summary>Occurs when a document item is opened.</summary>
    event EventHandler<ItemOpenedEventArgs>? ItemOpened;

    /// <summary>Occurs when a document item is closed.</summary>
    event EventHandler<ItemClosedEventArgs>? ItemClosed;

    /// <summary>Occurs when an item becomes selected in a group.</summary>
    event EventHandler<ItemActivatedEventArgs>? ItemActivated;

    /// <summary>Occurs when a tool item is created.</summary>
    event EventHandler<ToolCreatedEventArgs>? ToolCreated;

    /// <summary>Occurs when a tool item is removed.</summary>
    event EventHandler<ToolRemovedEventArgs>? ToolRemoved;

    /// <summary>Occurs after a persisted workspace is restored.</summary>
    event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;
}
