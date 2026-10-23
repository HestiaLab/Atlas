namespace Atlas.Blazor.Components;

/// <summary>
///     Configures browser-side splitter interaction.
/// </summary>
public sealed class AtlasSplitterOptions
{
    /// <summary>
    ///     Initializes splitter interaction options; the keyboard step must be
    ///     positive and finite, and the commit delay cannot be negative.
    /// </summary>
    public AtlasSplitterOptions(
        double keyboardStepPixels = 10d,
        int keyboardCommitDelayMilliseconds = 500)
    {
        if (!double.IsFinite(keyboardStepPixels) || keyboardStepPixels <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(keyboardStepPixels));
        }

        if (keyboardCommitDelayMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyboardCommitDelayMilliseconds));
        }

        KeyboardStepPixels = keyboardStepPixels;
        KeyboardCommitDelayMilliseconds = keyboardCommitDelayMilliseconds;
    }

    /// <summary>
    ///     Gets the CSS pixel change applied by one splitter arrow-key step.
    /// </summary>
    public double KeyboardStepPixels { get; }

    /// <summary>
    ///     Gets the debounce delay that terminates a keyboard resize burst.
    /// </summary>
    public int KeyboardCommitDelayMilliseconds { get; }
}

/// <summary>
///     Describes one splitter's transient presentation and allowed actions.
/// </summary>
public sealed class AtlasSplitterPresentation
{
    /// <summary>
    ///     Initializes a splitter presentation; CSS classes accept class-name
    ///     tokens only, and invalid characters throw.
    /// </summary>
    public AtlasSplitterPresentation(
        bool isLocked = false,
        string? cssClass = null,
        bool canCollapseFirst = false,
        bool canCollapseSecond = false)
    {
        CssClass = ValidateCssClass(cssClass);
        IsLocked = isLocked;
        CanCollapseFirst = canCollapseFirst;
        CanCollapseSecond = canCollapseSecond;
    }

    /// <summary>
    ///     Gets whether user interaction is disabled.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    ///     Gets the optional safe CSS class tokens.
    /// </summary>
    public string? CssClass { get; }

    /// <summary>
    ///     Gets whether the first child offers a collapse entry point.
    /// </summary>
    public bool CanCollapseFirst { get; }

    /// <summary>
    ///     Gets whether the second child offers a collapse entry point.
    /// </summary>
    public bool CanCollapseSecond { get; }

    private static string? ValidateCssClass(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(token => token.Any(character =>
            !(char.IsLetterOrDigit(character)
                || character is '-' or '_'))))
        {
            throw new ArgumentException(
                "Splitter CSS classes must contain only class-name tokens.",
                nameof(value));
        }

        return string.Join(" ", tokens);
    }
}
