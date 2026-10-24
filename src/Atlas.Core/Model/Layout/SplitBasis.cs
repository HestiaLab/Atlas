namespace Atlas.Core.Layout;

/// <summary>
///     分割的已提交尺寸基准：将一个子项锚定为固定像素，或按比例分配；解析器据
///     此推导偏好尺寸，解析过程中不修改基准。
/// </summary>
public readonly struct SplitBasis : IEquatable<SplitBasis>
{
    // 基准种类：FixedPixels 或 Proportional。
    private readonly SplitBasisKind _kind;

    // 基准数值：固定像素时为锚定子项尺寸，比例为第一子项占比。
    private readonly double _value;

    // 固定像素基准锚定的子项；比例基准恒为 null。
    private readonly SplitAnchor? _anchor;

    // 是否经工厂方法构造；default 实例为 false，属性访问抛异常。
    private readonly bool _isInitialized;

    private SplitBasis(SplitBasisKind kind, double value, SplitAnchor? anchor)
    {
        _kind = kind;
        _value = value;
        _anchor = anchor;
        _isInitialized = true;
    }

    /// <summary>
    ///     基准种类（FixedPixels 或 Proportional）；默认实例不携带数据，访问时
    ///     抛出异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例为默认的未初始化值。
    /// </exception>
    public SplitBasisKind Kind
    {
        get
        {
            EnsureInitialized();
            return _kind;
        }
    }

    /// <summary>
    ///     基准数值：FixedPixels 时为锚定子项的像素尺寸，Proportional 时为第一
    ///     个子项的占比。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例为默认的未初始化值。
    /// </exception>
    public double Value
    {
        get
        {
            EnsureInitialized();
            return _value;
        }
    }

    /// <summary>
    ///     FixedPixels 基准锚定的子项；Proportional 基准为
    ///     <see langword="null"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例为默认的未初始化值。
    /// </exception>
    public SplitAnchor? Anchor
    {
        get
        {
            EnsureInitialized();
            return _anchor;
        }
    }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认的未初始化值。
    /// </summary>
    public bool IsDefault => !_isInitialized;

    /// <summary>
    ///     创建固定像素基准：锚定子项占用 pixels（CSS 像素），另一子项占用剩余
    ///     空间；pixels 必须有限且非负，anchor 必须是已知值。
    /// </summary>
    /// <param name="pixels">有限且非负的子项尺寸。</param>
    /// <param name="anchor">尺寸固定的子项。</param>
    /// <returns>
    ///     构造的 SplitBasis 实例。
    /// </returns>
    public static SplitBasis FixedPixels(double pixels, SplitAnchor anchor)
    {
        ValidateFiniteNonNegative(pixels, nameof(pixels));
        ValidateAnchor(anchor);

        return new SplitBasis(SplitBasisKind.FixedPixels, pixels, anchor);
    }

    /// <summary>
    ///     创建比例基准：第一个子项占 firstRatio，第二个子项占剩余
    ///     (1 - firstRatio)；比例必须严格位于开区间 (0, 1) 内。
    /// </summary>
    /// <param name="firstRatio">大于零且小于一的有限比例。</param>
    /// <returns>
    ///     构造的 SplitBasis 实例。
    /// </returns>
    public static SplitBasis Proportional(double firstRatio)
    {
        if (!double.IsFinite(firstRatio) || firstRatio <= 0d || firstRatio >= 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstRatio),
                firstRatio,
                "A split proportion must be finite and greater than zero and less than one.");
        }

        return new SplitBasis(SplitBasisKind.Proportional, firstRatio, null);
    }

    /// <inheritdoc />
    public bool Equals(SplitBasis other)
    {
        return _isInitialized == other._isInitialized
            && _kind == other._kind
            && _value.Equals(other._value)
            && _anchor == other._anchor;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SplitBasis other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(_kind, _value, _anchor, _isInitialized);
    }

    /// <summary>
    ///     按种类、数值、锚定子项与初始化状态判断两个分割基准是否相等。
    /// </summary>
    public static bool operator ==(SplitBasis left, SplitBasis right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     按种类、数值、锚定子项与初始化状态判断两个分割基准是否不相等。
    /// </summary>
    public static bool operator !=(SplitBasis left, SplitBasis right)
    {
        return !left.Equals(right);
    }

    private static void ValidateFiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The extent must be finite and non-negative.");
        }
    }

    private static void ValidateAnchor(SplitAnchor anchor)
    {
        if (anchor is not SplitAnchor.First and not SplitAnchor.Second)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anchor),
                anchor,
                "Unknown split anchor.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("A default SplitBasis does not contain sizing data.");
        }
    }
}
