namespace Atlas.Core;

/// <summary>
///     Defines bounded workspace history behavior.
/// </summary>
public sealed class WorkspaceHistoryOptions
{
    /// <summary>
    ///     Creates history options; maxEntries and approximateMaxBytes must be
    ///     positive, and the coalescing window cannot be negative (default 500 ms).
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
    ///     Gets the maximum number of retained undo entries.
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    ///     Gets the optional approximate history byte budget. Patch sizes are
    ///     estimated from the target snapshot; the oldest patches are evicted
    ///     when the budget is exceeded. `HistoryNotRecorded` warning/barrier
    ///     semantics are not exposed yet; only the budget is enforced.
    /// </summary>
    public long? ApproximateMaxBytes { get; }

    /// <summary>
    ///     Gets the keyboard-resize coalescing window.
    /// </summary>
    public TimeSpan ResizeCoalescingWindow { get; }
}
