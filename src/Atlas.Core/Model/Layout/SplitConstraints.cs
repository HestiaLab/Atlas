namespace Atlas.Core.Layout;

/// <summary>
///     Defines the minimum and maximum sizes of the two children of a split; used during resolution to derive the feasible range and to clamp.
/// </summary>
public readonly struct SplitConstraints : IEquatable<SplitConstraints>
{
    /// <summary>
    ///     Constructs a constraint set; minimums must be finite and non-negative, and maximums must be finite and not less than the corresponding minimum.
    /// </summary>
    /// <param name="firstMinimum">A finite, non-negative minimum size for the first child.</param>
    /// <param name="firstMaximum">An optional finite maximum size for the first child.</param>
    /// <param name="secondMinimum">A finite, non-negative minimum size for the second child.</param>
    /// <param name="secondMaximum">An optional finite maximum size for the second child.</param>
    public SplitConstraints(
        double firstMinimum = 0d,
        double? firstMaximum = null,
        double secondMinimum = 0d,
        double? secondMaximum = null)
    {
        ValidateRange(firstMinimum, firstMaximum, nameof(firstMinimum), nameof(firstMaximum));
        ValidateRange(secondMinimum, secondMaximum, nameof(secondMinimum), nameof(secondMaximum));

        FirstMinimum = firstMinimum;
        FirstMaximum = firstMaximum;
        SecondMinimum = secondMinimum;
        SecondMaximum = secondMaximum;
    }

    /// <summary>
    ///     The minimum size of the first child (finite and non-negative).
    /// </summary>
    public double FirstMinimum { get; }

    /// <summary>
    ///     The maximum size of the first child; <see langword="null"/> means no upper bound.
    /// </summary>
    public double? FirstMaximum { get; }

    /// <summary>
    ///     The minimum size of the second child (finite and non-negative).
    /// </summary>
    public double SecondMinimum { get; }

    /// <summary>
    ///     The maximum size of the second child; <see langword="null"/> means no upper bound.
    /// </summary>
    public double? SecondMaximum { get; }

    /// <inheritdoc />
    public bool Equals(SplitConstraints other)
    {
        return FirstMinimum.Equals(other.FirstMinimum)
            && Nullable.Equals(FirstMaximum, other.FirstMaximum)
            && SecondMinimum.Equals(other.SecondMinimum)
            && Nullable.Equals(SecondMaximum, other.SecondMaximum);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SplitConstraints other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            FirstMinimum,
            FirstMaximum,
            SecondMinimum,
            SecondMaximum);
    }

    /// <summary>
    ///     Determines whether two constraint sets are equal.
    /// </summary>
    public static bool operator ==(SplitConstraints left, SplitConstraints right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two constraint sets are unequal.
    /// </summary>
    public static bool operator !=(SplitConstraints left, SplitConstraints right)
    {
        return !left.Equals(right);
    }

    private static void ValidateRange(
        double minimum,
        double? maximum,
        string minimumParameterName,
        string maximumParameterName)
    {
        if (!double.IsFinite(minimum) || minimum < 0d)
        {
            throw new ArgumentOutOfRangeException(
                minimumParameterName,
                minimum,
                "A minimum extent must be finite and non-negative.");
        }

        if (maximum is null)
        {
            return;
        }

        if (!double.IsFinite(maximum.Value) || maximum.Value < minimum)
        {
            throw new ArgumentOutOfRangeException(
                maximumParameterName,
                maximum,
                "A maximum extent must be finite and greater than or equal to its minimum.");
        }
    }
}
