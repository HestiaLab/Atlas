namespace Atlas.Core.Layout;

/// <summary>
///     Internal split node of the layout tree: divides the available space between two child nodes along
///     an axis, where the Basis determines the preferred size and the Constraints determine the limits.
/// </summary>
public sealed class SplitNode : LayoutNode
{
    /// <summary>
    ///     Constructs a split node; the axis must be known, the basis must not be default, and the two child
    ///     identifiers must be non-default and different; an argument exception is thrown otherwise.
    /// </summary>
    /// <param name="id">The node identifier within the workspace scope.</param>
    /// <param name="axis">The logical arrangement axis of the children.</param>
    /// <param name="basis">The committed sizing basis.</param>
    /// <param name="constraints">The child size constraints.</param>
    /// <param name="firstId">The identifier of the first child.</param>
    /// <param name="secondId">The identifier of the second child.</param>
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
    ///     The logical arrangement axis of the children.
    /// </summary>
    public SplitAxis Axis { get; }

    /// <summary>
    ///     The committed sizing basis.
    /// </summary>
    public SplitBasis Basis { get; }

    /// <summary>
    ///     The child size constraints.
    /// </summary>
    public SplitConstraints Constraints { get; }

    /// <summary>
    ///     The identifier of the first child node.
    /// </summary>
    public LayoutNodeId FirstId { get; }

    /// <summary>
    ///     The identifier of the second child node.
    /// </summary>
    public LayoutNodeId SecondId { get; }
}
