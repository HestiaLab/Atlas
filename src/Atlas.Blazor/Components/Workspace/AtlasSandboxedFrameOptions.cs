namespace Atlas.Blazor.Components;

/// <summary>
///     为不可信内容 iframe 配置固定的默认拒绝沙箱策略。
/// </summary>
public sealed class AtlasSandboxedFrameOptions
{
    /// <summary>
    ///     配置不可信内容 iframe；source 与 title 不得为空，消息大小限制必须
    ///     位于 256–1048576 字节之间。
    /// </summary>
    /// <param name="source">iframe 源 URL。</param>
    /// <param name="title">iframe 的可访问标题。</param>
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
    ///     获取 iframe 源 URL。
    /// </summary>
    public string Source { get; }

    /// <summary>
    ///     获取 iframe 的可访问标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     获取可接受的最大 UTF-8 消息大小。
    /// </summary>
    public int MaxMessageBytes { get; }
}
