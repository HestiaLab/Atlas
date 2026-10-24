namespace Atlas.Core.Layout;

/// <summary>
///     布局树的内部划分节点：沿某一轴在两个子节点之间分配可用空间，Basis 决定
///     偏好尺寸，Constraints 决定限制。
/// </summary>
public sealed class SplitNode : LayoutNode
{
    /// <summary>
    ///     构造分割节点；轴必须是已知值，基准不得为默认值，两个子标识符必须
    ///     非默认且不同；否则抛出参数异常。
    /// </summary>
    /// <param name="id">工作区范围内的节点标识符。</param>
    /// <param name="axis">子项的逻辑排列轴。</param>
    /// <param name="basis">已提交的尺寸基准。</param>
    /// <param name="constraints">子项尺寸约束。</param>
    /// <param name="firstId">第一个子项的标识符。</param>
    /// <param name="secondId">第二个子项的标识符。</param>
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
    ///     子项的逻辑排列轴。
    /// </summary>
    public SplitAxis Axis { get; }

    /// <summary>
    ///     已提交的尺寸基准。
    /// </summary>
    public SplitBasis Basis { get; }

    /// <summary>
    ///     子项尺寸约束。
    /// </summary>
    public SplitConstraints Constraints { get; }

    /// <summary>
    ///     第一个子节点的标识符。
    /// </summary>
    public LayoutNodeId FirstId { get; }

    /// <summary>
    ///     第二个子节点的标识符。
    /// </summary>
    public LayoutNodeId SecondId { get; }
}
