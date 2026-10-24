namespace Atlas.Core.Operations;

/// <summary>
///     原子改变工作区快照的不可变请求。
/// </summary>
public abstract class LayoutOperation
{
    private protected LayoutOperation(LayoutOperationKind kind, long? baseRevision)
    {
        if (kind is < LayoutOperationKind.OpenItem or > LayoutOperationKind.EqualizeSplit)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind.");
        }

        if (baseRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseRevision),
                baseRevision,
                "A base revision cannot be negative.");
        }

        Kind = kind;
        BaseRevision = baseRevision;
    }

    /// <summary>
    ///     获取稳定的操作种类。
    /// </summary>
    public LayoutOperationKind Kind { get; }

    /// <summary>
    ///     获取可选的乐观并发修订号：非 null 时在提交前校验，与当前已提交修订号
    ///     不一致时以 StaleOperation 失败，且不会覆盖其他提交。
    /// </summary>
    public long? BaseRevision { get; }
}
