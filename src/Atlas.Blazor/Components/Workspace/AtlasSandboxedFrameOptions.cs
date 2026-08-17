namespace Atlas.Blazor.Components;

/// <summary>
///     使用固定的默认拒绝沙箱策略配置不可信内容 iframe。
/// </summary>
public sealed class AtlasSandboxedFrameOptions
{
    /// <summary>
    ///     配置不可信内容 iframe；源与标题不能为空白，消息大小限制在 256–1048576 字节。
    /// </summary>
    /// <param name="source">框架源 URL。</param>
    /// <param name="title">可访问的框架标题。</param>
    /// <param name="maxMessageBytes">可接受的最大 UTF-8 消息大小。</param>
    public AtlasSandboxedFrameOptions(
        string source,
        string title,
        int maxMessageBytes = 65536)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentOutOfRangeException(nameof(title));
        }

        if (maxMessageBytes < 256 || maxMessageBytes > 1048576)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMessageBytes));
        }

        Source = source;
        Title = title;
        MaxMessageBytes = maxMessageBytes;
    }

    /// <summary>
    ///     获取框架源 URL。
    /// </summary>
    public string Source { get; }

    /// <summary>
    ///     获取可访问的框架标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     获取可接受的最大 UTF-8 消息大小。
    /// </summary>
    public int MaxMessageBytes { get; }
}
