namespace Atlas.Core.Layout;

/// <summary>
/// Represents a two-child spatial division in the layout tree.
/// </summary>
public sealed class SplitNode : LayoutNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplitNode"/> class.
    /// </summary>
    /// <param name="id">The workspace-scoped node identity.</param>
    /// <param name="axis">The logical child arrangement axis.</param>
    /// <param name="basis">The committed sizing basis.</param>
    /// <param name="constraints">The child sizing constraints.</param>
    /// <param name="firstId">The first child identity.</param>
    /// <param name="secondId">The second child identity.</param>
    public SplitNode(
        LayoutNodeId id,
        SplitAxis axis,
        SplitBasis basis,
        SplitConstraints constraints,
        LayoutNodeId firstId,
        LayoutNodeId secondId)
        : base(id)
    {
        if (axis is not SplitAxis.InlineChildren and not SplitAxis.BlockChildren)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown split axis.");
        }

        if (basis.IsDefault)
        {
            throw new ArgumentException(
                "A split basis cannot be the default value.",
                nameof(basis));
        }

        if (firstId == default)
        {
            throw new ArgumentException(
                "The first child identifier cannot be the default value.",
                nameof(firstId));
        }

        if (secondId == default)
        {
            throw new ArgumentException(
                "The second child identifier cannot be the default value.",
                nameof(secondId));
        }

        if (firstId == secondId)
        {
            throw new ArgumentException(
                "A split must reference two different child nodes.",
                nameof(secondId));
        }

        Axis = axis;
        Basis = basis;
        Constraints = constraints;
        FirstId = firstId;
        SecondId = secondId;
    }

    /// <summary>
    /// Gets the logical child arrangement axis.
    /// </summary>
    public SplitAxis Axis { get; }

    /// <summary>
    /// Gets the committed sizing basis.
    /// </summary>
    public SplitBasis Basis { get; }

    /// <summary>
    /// Gets the child sizing constraints.
    /// </summary>
    public SplitConstraints Constraints { get; }

    /// <summary>
    /// Gets the first child identity.
    /// </summary>
    public LayoutNodeId FirstId { get; }

    /// <summary>
    /// Gets the second child identity.
    /// </summary>
    public LayoutNodeId SecondId { get; }
}
