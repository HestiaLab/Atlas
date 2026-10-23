namespace Atlas.Core.Layout;

/// <summary>
///     Abstract base class of layout tree nodes; the tree consists of <see cref="GroupNode"/> leaves and
///     <see cref="SplitNode"/> splits, and construction validates that the node identifier is not the default value.
/// </summary>
public abstract class LayoutNode
{
    private protected LayoutNode(LayoutNodeId id)
    {
        if (id == default)
        {
            throw new ArgumentException(
                "A layout node identifier cannot be the default value.",
                nameof(id));
        }

        Id = id;
    }

    /// <summary>
    ///     The node identifier within the workspace scope.
    /// </summary>
    public LayoutNodeId Id { get; }
}
