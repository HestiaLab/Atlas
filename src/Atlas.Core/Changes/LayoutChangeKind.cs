namespace Atlas.Core.Changes;

/// <summary>
///     标识一次已提交布局变更的语义来源。
/// </summary>
public enum LayoutChangeKind
{
    /// <summary>
    ///     直接执行的布局操作。
    /// </summary>
    Operation = 0,

    /// <summary>
    ///     一次撤销历史遍历。
    /// </summary>
    Undo = 1,

    /// <summary>
    ///     一次重做历史遍历。
    /// </summary>
    Redo = 2,

    /// <summary>
    ///     重置为配置的工作区定义。
    /// </summary>
    Reset = 3,

    /// <summary>
    ///     一次持久化工作区恢复。
    /// </summary>
    Restore = 4,

    /// <summary>
    ///     与已变更工作区定义进行对账。
    /// </summary>
    ReconcileDefinition = 5,
}
