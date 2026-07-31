namespace Atlas.Core.Layout;

/// <summary>
/// Stores the committed sizing basis for a two-child split.
/// </summary>
public readonly struct SplitBasis : IEquatable<SplitBasis>
{
    private readonly SplitBasisKind _kind;
    private readonly double _value;
    private readonly SplitAnchor? _anchor;
    private readonly bool _isInitialized;

    private SplitBasis(SplitBasisKind kind, double value, SplitAnchor? anchor)
    {
        _kind = kind;
        _value = value;
        _anchor = anchor;
        _isInitialized = true;
    }

    /// <summary>
    /// Gets the basis kind.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
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
    /// Gets the fixed extent or first-child proportion represented by this basis.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
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
    /// Gets the fixed child, or <see langword="null"/> for a proportional basis.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
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
    /// Creates a basis that fixes one child to an extent measured in CSS pixels.
    /// </summary>
    /// <param name="pixels">The finite, non-negative child extent.</param>
    /// <param name="anchor">The child whose extent is fixed.</param>
    /// <returns>The validated split basis.</returns>
    public static SplitBasis FixedPixels(double pixels, SplitAnchor anchor)
    {
        ValidateFiniteNonNegative(pixels, nameof(pixels));
        ValidateAnchor(anchor);

        return new SplitBasis(SplitBasisKind.FixedPixels, pixels, anchor);
    }

    /// <summary>
    /// Creates a basis in which the first child occupies the specified proportion.
    /// </summary>
    /// <param name="firstRatio">A finite ratio greater than zero and less than one.</param>
    /// <returns>The validated split basis.</returns>
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
    /// Determines whether two split bases are equal.
    /// </summary>
    public static bool operator ==(SplitBasis left, SplitBasis right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two split bases are not equal.
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
