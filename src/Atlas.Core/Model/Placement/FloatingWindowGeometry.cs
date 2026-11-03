namespace Atlas.Core.Placement;

/// <summary>
///     浮动窗口的已提交几何：宿主相对 CSS px 的位置与尺寸。默认实例不携带
///     数据，属性访问时抛出异常。
/// </summary>
public readonly struct FloatingWindowGeometry : IEquatable<FloatingWindowGeometry>
{
    private readonly double _x;
    private readonly double _y;
    private readonly double _width;
    private readonly double _height;
    private readonly bool _isInitialized;

    /// <summary>
    ///     构造几何；坐标必须有限，宽度和高度必须有限且不小于最小尺寸。
    /// </summary>
    /// <param name="x">宿主相对水平位置。</param>
    /// <param name="y">宿主相对垂直位置。</param>
    /// <param name="width">窗口宽度。</param>
    /// <param name="height">窗口高度。</param>
    public FloatingWindowGeometry(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                x,
                "A floating-window x coordinate must be finite.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(y),
                y,
                "A floating-window y coordinate must be finite.");
        }

        if (!double.IsFinite(width) || width < FloatingWindowLimits.MinimumWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "A floating-window width must be finite and at least the minimum width.");
        }

        if (!double.IsFinite(height) || height < FloatingWindowLimits.MinimumHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "A floating-window height must be finite and at least the minimum height.");
        }

        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _isInitialized = true;
    }

    /// <summary>
    ///     宿主相对水平位置。
    /// </summary>
    /// <exception cref="InvalidOperationException">该实例为默认的未初始化值。</exception>
    public double X
    {
        get
        {
            EnsureInitialized();
            return _x;
        }
    }

    /// <summary>
    ///     宿主相对垂直位置。
    /// </summary>
    /// <exception cref="InvalidOperationException">该实例为默认的未初始化值。</exception>
    public double Y
    {
        get
        {
            EnsureInitialized();
            return _y;
        }
    }

    /// <summary>
    ///     窗口宽度（不小于 <see cref="FloatingWindowLimits.MinimumWidth"/>）。
    /// </summary>
    /// <exception cref="InvalidOperationException">该实例为默认的未初始化值。</exception>
    public double Width
    {
        get
        {
            EnsureInitialized();
            return _width;
        }
    }

    /// <summary>
    ///     窗口高度（不小于 <see cref="FloatingWindowLimits.MinimumHeight"/>）。
    /// </summary>
    /// <exception cref="InvalidOperationException">该实例为默认的未初始化值。</exception>
    public double Height
    {
        get
        {
            EnsureInitialized();
            return _height;
        }
    }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认的未初始化值。
    /// </summary>
    public bool IsDefault => !_isInitialized;

    /// <summary>
    ///     创建默认尺寸、位于原点的几何。
    /// </summary>
    public static FloatingWindowGeometry Default => new(
        0d,
        0d,
        FloatingWindowLimits.DefaultWidth,
        FloatingWindowLimits.DefaultHeight);

    /// <inheritdoc />
    public bool Equals(FloatingWindowGeometry other)
    {
        return _x.Equals(other._x)
            && _y.Equals(other._y)
            && _width.Equals(other._width)
            && _height.Equals(other._height)
            && _isInitialized == other._isInitialized;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FloatingWindowGeometry other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(_x, _y, _width, _height, _isInitialized);
    }

    /// <summary>
    ///     比较两个几何是否相等。
    /// </summary>
    public static bool operator ==(
        FloatingWindowGeometry left,
        FloatingWindowGeometry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     比较两个几何是否不相等。
    /// </summary>
    public static bool operator !=(
        FloatingWindowGeometry left,
        FloatingWindowGeometry right)
    {
        return !left.Equals(right);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "A default floating-window geometry has no coordinates or size.");
        }
    }
}
