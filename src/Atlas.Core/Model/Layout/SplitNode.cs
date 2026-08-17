namespace Atlas.Core.Layout;

/// <summary>
///     布局树的内部分割节点：按轴把可用空间分给两个子节点，
///     尺寸由 Basis 决定首选、Constraints 决定约束。
/// </summary>
public sealed class SplitNode : LayoutNode
{
    /// <summary>
    ///     构造分割节点；轴必须已知、基准不得为默认值、
    ///     两个子节点标识不得为默认值且不得相同，违反时抛参数异常。
    /// </summary>
    /// <param name="id">工作区作用域内的节点标识。</param>
    /// <param name="axis">子项的逻辑排列轴。</param>
    /// <param name="basis">已提交的尺寸基准。</param>
    /// <param name="constraints">子项尺寸约束。</param>
    /// <param name="firstId">第一个子项的标识。</param>
    /// <param name="secondId">第二个子项的标识。</param>
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
    ///     第一个子节点的标识。
    /// </summary>
    public LayoutNodeId FirstId { get; }

    /// <summary>
    ///     第二个子节点的标识。
    /// </summary>
    public LayoutNodeId SecondId { get; }
}
