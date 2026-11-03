namespace Atlas.Core.Placement;

/// <summary>
///     浮动窗口的稳定资源限制与默认几何。
/// </summary>
public static class FloatingWindowLimits
{
    /// <summary>
    ///     同时打开的最大浮动窗口数量。
    /// </summary>
    public const int MaximumOpenWindows = 8;

    /// <summary>
    ///     浮动窗口的最小宽度（宿主相对 CSS px）。
    /// </summary>
    public const double MinimumWidth = 240d;

    /// <summary>
    ///     浮动窗口的最小高度（宿主相对 CSS px）。
    /// </summary>
    public const double MinimumHeight = 160d;

    /// <summary>
    ///     浮动窗口的默认宽度（宿主相对 CSS px）。
    /// </summary>
    public const double DefaultWidth = 360d;

    /// <summary>
    ///     浮动窗口的默认高度（宿主相对 CSS px）。
    /// </summary>
    public const double DefaultHeight = 480d;
}
