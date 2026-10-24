namespace Atlas.Core.Persistence;

/// <summary>
///     提供隔离的内存自动布局存储。
/// </summary>
public sealed class InMemoryWorkspaceLayoutStore : IWorkspaceLayoutStore
{
    // 文档槽的互斥锁。
    private readonly object gate = new();

    // 最近一次保存的布局文档；null 表示从未保存过。
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
