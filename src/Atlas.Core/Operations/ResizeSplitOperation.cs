using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>
///     为现有分屏提交已解析的尺寸基准。
/// </summary>
public sealed class ResizeSplitOperation : LayoutOperation
{
    /// <summary>
    ///     初始化分屏尺寸调整操作。
    /// </summary>
    public ResizeSplitOperation(
        LayoutNodeId splitId,
        SplitBasis basis,
        long? baseRevision = null)
        : this(splitId, basis, ResizeSplitInputKind.Pointer, baseRevision)
    {
    }

    /// <summary>
    ///     为历史合并创建一个键盘尺寸调整步骤。
    /// </summary>
    public static ResizeSplitOperation CreateKeyboard(
        LayoutNodeId splitId,
        SplitBasis basis)
    {
        return new ResizeSplitOperation(
            splitId,
            basis,
            ResizeSplitInputKind.Keyboard,
            baseRevision: null);
    }

    private ResizeSplitOperation(
        LayoutNodeId splitId,
        SplitBasis basis,
        ResizeSplitInputKind inputKind,
        long? baseRevision)
        : base(LayoutOperationKind.ResizeSplit, baseRevision)
    {
        if (splitId == default)
        {
            throw new ArgumentException(
                "A split identifier cannot be the default value.",
                nameof(splitId));
        }

        if (basis.IsDefault)
        {
            throw new ArgumentException(
                "A split basis cannot be the default value.",
                nameof(basis));
        }

        if (!Enum.IsDefined(typeof(ResizeSplitInputKind), inputKind))
        {
            throw new ArgumentOutOfRangeException(nameof(inputKind));
        }

        SplitId = splitId;
        Basis = basis;
        InputKind = inputKind;
    }

    /// <summary>
    ///     获取要调整尺寸的分屏。
    /// </summary>
    public LayoutNodeId SplitId { get; }

    /// <summary>
    ///     获取要提交的已解析基准。
    /// </summary>
    public SplitBasis Basis { get; }

    /// <summary>
    ///     获取输入来源；键盘调整参与撤销历史合并，指针调整不参与。
    /// </summary>
    public ResizeSplitInputKind InputKind { get; }
}
