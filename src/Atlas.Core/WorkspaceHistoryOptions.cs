namespace Atlas.Core;

/// <summary>Defines bounded workspace history behavior.</summary>
public sealed class WorkspaceHistoryOptions
{
    /// <summary>Initializes history options.</summary>
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

    /// <summary>Gets the maximum retained undo entry count.</summary>
    public int MaxEntries { get; }

    /// <summary>Gets the optional approximate history byte budget.</summary>
    public long? ApproximateMaxBytes { get; }

    /// <summary>Gets the keyboard resize coalescing window.</summary>
    public TimeSpan ResizeCoalescingWindow { get; }
}
