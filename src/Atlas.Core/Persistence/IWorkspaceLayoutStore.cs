namespace Atlas.Core.Persistence;

/// <summary>Stores the reserved automatic layout document for one workspace.</summary>
public interface IWorkspaceLayoutStore
{
    /// <summary>Saves or overwrites the automatic document.</summary>
    ValueTask SaveAsync(
        WorkspaceLayoutDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the automatic document, or null when none exists.</summary>
    ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
        CancellationToken cancellationToken = default);
}
