namespace Atlas.Core.Layout;

/// <summary>
///     The committed sizing basis of a split: anchor one child to fixed pixels, or distribute proportionally;
///     the resolver derives preferred sizes from it and does not modify the basis while resolving.
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
    ///     Basis kind (FixedPixels or Proportional); a default instance carries no data and throws on access.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
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
    ///     Basis value: the pixel size of the anchored child for FixedPixels, or the share of the first child for Proportional.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
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
    ///     The child anchored by a FixedPixels basis; <see langword="null"/> for a Proportional basis.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
    /// </exception>
    public SplitAnchor? Anchor
    {
        get
        {
            EnsureInitialized();
            return _anchor;
        }
    }

    internal bool IsDefault => !_isInitialized;

    /// <summary>
    ///     Creates a fixed-pixel basis: the anchored child occupies pixels (CSS pixels) while the other child
    ///     takes the remaining space; pixels must be finite and non-negative and anchor must be a known value.
    /// </summary>
    /// <param name="pixels">A finite, non-negative child size.</param>
    /// <param name="anchor">The child whose size is fixed.</param>
    /// <returns>
    ///     The constructed SplitBasis instance.
    /// </returns>
    public static SplitBasis FixedPixels(double pixels, SplitAnchor anchor)
    {
        ValidateFiniteNonNegative(pixels, nameof(pixels));
        ValidateAnchor(anchor);

        return new SplitBasis(SplitBasisKind.FixedPixels, pixels, anchor);
    }

    /// <summary>
    ///     Creates a proportional basis: the first child takes firstRatio and the second takes the remaining
    ///     (1 - firstRatio); the ratio must lie strictly within the open interval (0, 1).
    /// </summary>
    /// <param name="firstRatio">A finite ratio greater than zero and less than one.</param>
    /// <returns>
    ///     The constructed SplitBasis instance.
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
    ///     Compares two split bases for equality by kind, value, anchor, and initialization state.
    /// </summary>
    public static bool operator ==(SplitBasis left, SplitBasis right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two split bases for inequality by kind, value, anchor, and initialization state.
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
