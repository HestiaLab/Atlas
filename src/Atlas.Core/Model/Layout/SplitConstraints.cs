namespace Atlas.Core.Layout;

/// <summary>
/// Defines the minimum and maximum extents for both children of a split.
/// </summary>
public readonly struct SplitConstraints : IEquatable<SplitConstraints>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplitConstraints"/> struct.
    /// </summary>
    /// <param name="firstMinimum">The finite, non-negative first-child minimum.</param>
    /// <param name="firstMaximum">The optional finite first-child maximum.</param>
    /// <param name="secondMinimum">The finite, non-negative second-child minimum.</param>
    /// <param name="secondMaximum">The optional finite second-child maximum.</param>
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
    /// Gets the first-child minimum extent.
    /// </summary>
    public double FirstMinimum { get; }

    /// <summary>
    /// Gets the optional first-child maximum extent.
    /// </summary>
    public double? FirstMaximum { get; }

    /// <summary>
    /// Gets the second-child minimum extent.
    /// </summary>
    public double SecondMinimum { get; }

    /// <summary>
    /// Gets the optional second-child maximum extent.
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
    /// Determines whether two constraint sets are equal.
    /// </summary>
    public static bool operator ==(SplitConstraints left, SplitConstraints right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two constraint sets are not equal.
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
