using Atlas.Core.Changes;

namespace Atlas.Core.Results;

/// <summary>
///     打开请求的结果：返回实际生效的条目标识符（可能是工作区生成的 GUID）、
///     是否新增了条目，以及本次提交的变更集。
/// </summary>
public sealed class OpenItemResult
{
    internal OpenItemResult(
        DockItemId itemId,
        bool created,
        LayoutChangeSet changeSet)
    {
        ItemId = itemId;
        Created = created;
        ChangeSet = changeSet;
    }

    /// <summary>
    ///     获取实际生效的条目标识符；去重命中时为既有条目的标识符。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取一个值，指示本次打开是否新增了条目；去重激活既有条目时为
    ///     <see langword="false"/>。
    /// </summary>
    public bool Created { get; }

    /// <summary>
    ///     获取本次打开提交的变更集。
    /// </summary>
    public LayoutChangeSet ChangeSet { get; }
}
