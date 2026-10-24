namespace Atlas.Core.Persistence;

/// <summary>
///     存储为一个工作区保留的自动布局文档。
/// </summary>
public interface IWorkspaceLayoutStore
{
    /// <summary>
    ///     保存或覆盖自动布局文档。
    /// </summary>
    ValueTask SaveAsync(
        WorkspaceLayoutDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     加载自动布局文档；不存在时返回 null。
    /// </summary>
    ValueTask<WorkspaceLayoutDocument?> TryLoadAsync(
        CancellationToken cancellationToken = default);
}
