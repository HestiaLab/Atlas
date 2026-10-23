namespace Atlas.Core.Persistence;

/// <summary>
///     Stores the automatic layout document retained for one workspace.
/// </summary>
public interface IWorkspaceLayoutStore
{
    /// <summary>
    ///     Saves or overwrites the automatic layout document.
    /// </summary>
    ValueTask SaveAsync(
        WorkspaceLayoutDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the automatic layout document, returning null when it does not exist.
    /// </summary>
    ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
        CancellationToken cancellationToken = default);
}
