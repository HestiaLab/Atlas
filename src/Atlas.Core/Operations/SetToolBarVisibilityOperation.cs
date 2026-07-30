using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

internal sealed class SetToolBarVisibilityOperation : LayoutOperation
{
    internal SetToolBarVisibilityOperation(
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

    internal LogicalSide Side { get; }

    internal ToolBarVisibility Visibility { get; }
}
