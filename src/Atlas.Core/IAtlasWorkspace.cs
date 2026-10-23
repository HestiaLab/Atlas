using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     Provides atomic access to one isolated Atlas workspace.
/// </summary>
public interface IAtlasWorkspace : IAsyncDisposable
{
    /// <summary>
    ///     Gets the stable workspace identifier.
    /// </summary>
    WorkspaceId Id { get; }

    /// <summary>
    ///     Gets the latest committed immutable snapshot.
    /// </summary>
    WorkspaceSnapshot Current { get; }

    /// <summary>
    ///     Gets a value indicating whether a committed operation can be undone.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    ///     Gets a value indicating whether an undone operation can be redone.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    ///     Gets the strongly typed events derived from committed changes.
    /// </summary>
    IAtlasWorkspaceDomainEvents DomainEvents { get; }

    /// <summary>
    ///     Occurs after a new snapshot is committed.
    /// </summary>
    event EventHandler<WorkspaceChangedEventArgs>? Changed;

    /// <summary>
    ///     Executes one operation atomically.
    /// </summary>
    /// <param name="operation">Immutable operation request.</param>
    /// <param name="cancellationToken">Token used to cancel pending asynchronous work.</param>
    /// <returns>
    ///     The committed change, a no-change result, or an expected failure.
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
        LayoutOperation operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moves one step backward through committed history.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel pending asynchronous work.</param>
    /// <returns>
    ///     The committed undo change, a no-change result, or an expected failure.
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moves one step forward through previously undone history.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel pending asynchronous work.</param>
    /// <returns>
    ///     The committed redo change, a no-change result, or an expected failure.
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
        CancellationToken cancellationToken = default);
}
