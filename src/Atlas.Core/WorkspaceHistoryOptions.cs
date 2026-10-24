namespace Atlas.Core;

/// <summary>
///     定义有界的工作区历史行为。
/// </summary>
public sealed class WorkspaceHistoryOptions
{
    /// <summary>
    ///     创建历史选项；maxEntries 与 approximateMaxBytes 必须为正数，合并
    ///     窗口不能为负数（默认 500 ms）。
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
    ///     获取保留的撤销条目上限。
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    ///     获取可选的近似历史字节预算。补丁大小根据目标快照估算；超出预算时
    ///     驱逐最旧的补丁。`HistoryNotRecorded` 警告/屏障语义尚未暴露；当前
    ///     只强制执行预算。
    /// </summary>
    public long? ApproximateMaxBytes { get; }

    /// <summary>
    ///     获取键盘调整尺寸的合并窗口。
    /// </summary>
    public TimeSpan ResizeCoalescingWindow { get; }
}
