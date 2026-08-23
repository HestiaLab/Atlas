namespace Atlas.Core.Persistence;

/// <summary>
///     存储命名的布局文档，不解释其内容。
/// </summary>
public interface ILayoutPresetStore
{
    /// <summary>
    ///     保存或覆盖一个序号名称对应的预设。
    /// </summary>
    ValueTask SaveAsync(
        string name,
        LayoutPresetDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     加载一个序号名称对应的预设，不存在时返回 null。
    /// </summary>
    ValueTask<LayoutPresetDocument?> TryLoadAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     删除一个序号名称对应的预设，并报告其是否存在。
    /// </summary>
    ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     列出已存储的用户预设描述符。
    /// </summary>
    ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);
}
