namespace Atlas.Core.Layout;

/// <summary>
///     Trusted metadata for tool item sizing: initial ratio, minimum/maximum content extent, and collapsed
///     extent; declared by the host, and the workspace uses it to initialize and constrain tool groups.
/// </summary>
public readonly struct ToolSizingDefinition : IEquatable<ToolSizingDefinition>
{
    /// <summary>
    ///     Constructs a sizing definition; the initial ratio must lie in (0, 1], extents must be finite and
    ///     non-negative, the maximum must not be less than the minimum, and the collapsed extent must be finite and non-negative.
    /// </summary>
    public ToolSizingDefinition(
        double? initialRatio = null,
        double? minExtent = null,
        double? maxExtent = null,
        double collapsedExtent = 0d)
    {
        if (initialRatio is not null
            && (!double.IsFinite(initialRatio.Value)
                || initialRatio.Value <= 0d
                || initialRatio.Value > 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialRatio),
                initialRatio,
                "An initial ratio must be finite and greater than zero and at most one.");
        }

        ValidateOptionalExtent(minExtent, nameof(minExtent));
        ValidateOptionalExtent(maxExtent, nameof(maxExtent));

        if (minExtent is not null
            && maxExtent is not null
            && maxExtent.Value < minExtent.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExtent),
                maxExtent,
                "A maximum extent cannot be less than the minimum extent.");
        }

        if (!double.IsFinite(collapsedExtent) || collapsedExtent < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collapsedExtent),
                collapsedExtent,
                "A collapsed extent must be finite and non-negative.");
        }

        InitialRatio = initialRatio;
        MinExtent = minExtent;
        MaxExtent = maxExtent;
        CollapsedExtent = collapsedExtent;
    }

    /// <summary>
    ///     The ratio used when initializing and resetting a tool group; <see langword="null"/> means no initial ratio is specified.
    /// </summary>
    public double? InitialRatio { get; }

    /// <summary>
    ///     The minimum content extent of a tool group; <see langword="null"/> means no lower bound.
    /// </summary>
    public double? MinExtent { get; }

    /// <summary>
    ///     The maximum content extent of a tool group; <see langword="null"/> means no upper bound.
    /// </summary>
    public double? MaxExtent { get; }

    /// <summary>
    ///     The extent a tool group occupies when collapsed; defaults to 0 (fully collapsed).
    /// </summary>
    public double CollapsedExtent { get; }

    /// <inheritdoc />
    public bool Equals(ToolSizingDefinition other)
    {
        return Nullable.Equals(InitialRatio, other.InitialRatio)
            && Nullable.Equals(MinExtent, other.MinExtent)
            && Nullable.Equals(MaxExtent, other.MaxExtent)
            && CollapsedExtent.Equals(other.CollapsedExtent);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ToolSizingDefinition other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(InitialRatio, MinExtent, MaxExtent, CollapsedExtent);
    }

    /// <summary>
    ///     Determines whether two sizing definitions are equal.
    /// </summary>
    public static bool operator ==(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two sizing definitions are unequal.
    /// </summary>
    public static bool operator !=(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return !left.Equals(right);
    }

    private static void ValidateOptionalExtent(double? value, string parameterName)
    {
        if (value is not null && (!double.IsFinite(value.Value) || value.Value < 0d))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "An extent must be finite and non-negative.");
        }
    }
}
