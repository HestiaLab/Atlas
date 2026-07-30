using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>Changes committed visibility for one logical toolbar.</summary>
public sealed class SetToolBarVisibilityOperation : LayoutOperation
{
    /// <summary>Initializes a toolbar visibility operation.</summary>
    public SetToolBarVisibilityOperation(
        LogicalSide side,
        ToolBarVisibility visibility,
        long? baseRevision = null)
        : base(LayoutOperationKind.SetToolBarVisibility, baseRevision)
    {
        if (side is not LogicalSide.InlineStart and not LogicalSide.InlineEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown logical side.");
        }

        if (visibility is not ToolBarVisibility.Expanded
            and not ToolBarVisibility.Collapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibility),
                visibility,
                "Unknown toolbar visibility.");
        }

        Side = side;
        Visibility = visibility;
    }

    /// <summary>Gets the logical toolbar side.</summary>
    public LogicalSide Side { get; }

    /// <summary>Gets the requested toolbar visibility.</summary>
    public ToolBarVisibility Visibility { get; }
}
