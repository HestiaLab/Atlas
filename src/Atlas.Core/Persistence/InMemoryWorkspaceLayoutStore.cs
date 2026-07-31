namespace Atlas.Core.Persistence;

/// <summary>Provides an isolated in-memory automatic layout store.</summary>
public sealed class InMemoryWorkspaceLayoutStore : IWorkspaceLayoutStore
{
    private readonly object gate = new();
    private WorkspaceLayoutDocument? document;

    /// <inheritdoc />
    public ValueTask SaveAsync(
        WorkspaceLayoutDocument document,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(document);
        lock (gate)
        {
            this.document = document;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return ValueTask.FromResult(document);
        }
    }
}
