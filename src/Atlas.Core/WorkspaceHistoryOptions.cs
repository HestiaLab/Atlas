namespace Atlas.Core;

/// <summary>
///     定义有界的工作区历史行为。
/// </summary>
public sealed class WorkspaceHistoryOptions
{
    /// <summary>
    ///     构造历史选项；maxEntries 与 approximateMaxBytes 必须为正，
    ///     合并窗口不能为负（缺省 500ms）。
    /// </summary>
    public WorkspaceHistoryOptions(
        int maxEntries = 100,
        long? approximateMaxBytes = null,
        TimeSpan? resizeCoalescingWindow = null)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        if (approximateMaxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(approximateMaxBytes));
        }

        var window = resizeCoalescingWindow ?? TimeSpan.FromMilliseconds(500);
        if (window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resizeCoalescingWindow));
        }

        MaxEntries = maxEntries;
        ApproximateMaxBytes = approximateMaxBytes;
        ResizeCoalescingWindow = window;
    }

    /// <summary>
    ///     获取保留的最大撤销条目数。
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    ///     获取可选的近似历史字节预算；按补丁的目标快照估算，超出时淘汰最旧补丁。
    ///     `HistoryNotRecorded` 警告/历史屏障语义尚未暴露，当前只保证预算生效。
    /// </summary>
    public long? ApproximateMaxBytes { get; }

    /// <summary>
    ///     获取键盘调整大小的合并窗口。
    /// </summary>
    public TimeSpan ResizeCoalescingWindow { get; }
}
