namespace Atlas.Blazor.Components;

/// <summary>
///     配置浏览器端分割条交互。
/// </summary>
public sealed class AtlasSplitterOptions
{
    /// <summary>
    ///     初始化分割条交互选项；方向键步进必须为正且有限，提交延迟不能为负。
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
    ///     获取分割条单次方向键步进的 CSS 像素变化量。
    /// </summary>
    public double KeyboardStepPixels { get; }

    /// <summary>
    ///     获取终止键盘调整连发的防抖延迟。
    /// </summary>
    public int KeyboardCommitDelayMilliseconds { get; }
}

/// <summary>
///     描述单个分割条的瞬态呈现与允许的操作。
/// </summary>
public sealed class AtlasSplitterPresentation
{
    /// <summary>
    ///     初始化分割条呈现；CSS 类仅接受类名令牌，非法字符抛异常。
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
    ///     获取是否禁用用户交互。
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    ///     获取可选的安全 CSS 类令牌。
    /// </summary>
    public string? CssClass { get; }

    /// <summary>
    ///     获取第一个子项是否提供折叠操作入口。
    /// </summary>
    public bool CanCollapseFirst { get; }

    /// <summary>
    ///     获取第二个子项是否提供折叠操作入口。
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
