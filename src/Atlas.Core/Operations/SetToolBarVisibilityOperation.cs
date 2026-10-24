using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     改变逻辑工具栏的已提交可见性。
/// </summary>
public sealed class SetToolBarVisibilityOperation : LayoutOperation
{
    /// <summary>
    ///     初始化工具栏可见性操作。
    /// </summary>
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

    /// <summary>
    ///     获取工具栏的逻辑侧。
    /// </summary>
    public LogicalSide Side { get; }

    /// <summary>
    ///     获取请求的工具栏可见性。
    /// </summary>
    public ToolBarVisibility Visibility { get; }
}
