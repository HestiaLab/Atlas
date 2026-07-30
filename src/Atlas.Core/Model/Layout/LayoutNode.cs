namespace Atlas.Core.Layout;

/// <summary>
/// Represents a node in the closed Atlas layout tree hierarchy.
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
    /// Gets the workspace-scoped node identity.
    /// </summary>
    public LayoutNodeId Id { get; }
}
