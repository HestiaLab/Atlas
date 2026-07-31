namespace Atlas.Core.Layout;

/// <summary>
/// Defines trusted initial and constraint metadata for a tool item.
/// </summary>
public readonly struct ToolSizingDefinition : IEquatable<ToolSizingDefinition>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSizingDefinition"/> struct.
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
    /// Gets the optional ratio used for initialization and reset.
    /// </summary>
    public double? InitialRatio { get; }

    /// <summary>
    /// Gets the optional minimum content extent.
    /// </summary>
    public double? MinExtent { get; }

    /// <summary>
    /// Gets the optional maximum content extent.
    /// </summary>
    public double? MaxExtent { get; }

    /// <summary>
    /// Gets the content extent used while collapsed.
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
    /// Determines whether two sizing definitions are equal.
    /// </summary>
    public static bool operator ==(ToolSizingDefinition left, ToolSizingDefinition right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two sizing definitions are not equal.
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
