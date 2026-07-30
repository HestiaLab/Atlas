using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
/// Provides atomic access to one isolated Atlas workspace.
/// </summary>
public interface IAtlasWorkspace : IAsyncDisposable
{
    /// <summary>Gets the stable workspace identity.</summary>
    WorkspaceId Id { get; }

    /// <summary>Gets the latest committed immutable snapshot.</summary>
    WorkspaceSnapshot Current { get; }

    /// <summary>Gets whether a committed operation can be undone.</summary>
    bool CanUndo { get; }

    /// <summary>Gets whether an undone operation can be redone.</summary>
    bool CanRedo { get; }

    /// <summary>Occurs after a new snapshot has been committed.</summary>
    event EventHandler<WorkspaceChangedEventArgs>? Changed;

    /// <summary>Executes one operation atomically.</summary>
    /// <param name="operation">The immutable operation request.</param>
    /// <param name="cancellationToken">A token that cancels pending asynchronous work.</param>
    /// <returns>The committed change, no-change outcome, or expected failure.</returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
        LayoutOperation operation,
        CancellationToken cancellationToken = default);

    /// <summary>Moves one step backward through committed history.</summary>
    /// <param name="cancellationToken">A token that cancels pending asynchronous work.</param>
    /// <returns>The committed undo change, no-change outcome, or expected failure.</returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Moves one step forward through previously undone history.</summary>
    /// <param name="cancellationToken">A token that cancels pending asynchronous work.</param>
    /// <returns>The committed redo change, no-change outcome, or expected failure.</returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
        CancellationToken cancellationToken = default);
}
