namespace Atlas.Blazor.Components;

/// <summary>
///     Configures an untrusted content iframe with a fixed deny-by-default
///     sandbox policy.
/// </summary>
public sealed class AtlasSandboxedFrameOptions
{
    /// <summary>
    ///     Configures the untrusted content iframe; the source and title cannot
    ///     be blank and the message size limit must be within 256-1048576
    ///     bytes.
    /// </summary>
    /// <param name="source">The frame source URL.</param>
    /// <param name="title">The accessible frame title.</param>
    /// <param name="maxMessageBytes">The maximum accepted UTF-8 message size.</param>
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
    ///     Gets the frame source URL.
    /// </summary>
    public string Source { get; }

    /// <summary>
    ///     Gets the accessible frame title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the maximum accepted UTF-8 message size.
    /// </summary>
    public int MaxMessageBytes { get; }
}
