namespace Atlas.Core.Operations;

/// <summary>
///     标识稳定的 Atlas 工作区操作种类。
/// </summary>
public enum LayoutOperationKind
{
    /// <summary>
    ///     打开一个项。
    /// </summary>
    OpenItem = 0,

    /// <summary>
    ///     关闭一个项。
    /// </summary>
    CloseItem = 1,

    /// <summary>
    ///     在其组中选择一个项。
    /// </summary>
    SelectItem = 2,

    /// <summary>
    ///     将一个项移动到不同的放置。
    /// </summary>
    MoveItem = 3,

    /// <summary>
    ///     在当前放置内重排一个项。
    /// </summary>
    ReorderItem = 4,

    /// <summary>
    ///     为一个项创建分屏放置。
    /// </summary>
    SplitItem = 5,

    /// <summary>
    ///     调整现有分屏的大小。
    /// </summary>
    ResizeSplit = 6,

    /// <summary>
    ///     更改组的可见性。
    /// </summary>
    SetGroupVisibility = 7,

    /// <summary>
    ///     将项移入或移出自动隐藏。
    /// </summary>
    SetAutoHide = 8,

    /// <summary>
    ///     将自动隐藏的项恢复到其已提交的停靠返回目标。
    /// </summary>
    RestoreAutoHiddenItem = 9,

    /// <summary>
    ///     更新项的呈现元数据。
    /// </summary>
    UpdateItemPresentation = 10,

    /// <summary>
    ///     替换项的内容引用。
    /// </summary>
    ReplaceContentReference = 11,

    /// <summary>
    ///     将工作区重置为其配置的定义。
    /// </summary>
    ResetWorkspace = 12,

    /// <summary>
    ///     恢复持久化的工作区状态。
    /// </summary>
    RestoreWorkspace = 13,

    /// <summary>
    ///     更改逻辑侧的工具栏可见性。
    /// </summary>
    SetToolBarVisibility = 14,

    /// <summary>
    ///     在逻辑区域内重排工具栏条目。
    /// </summary>
    ReorderToolBarEntry = 15,

    /// <summary>
    ///     将已提交状态与声明式定义对账。
    /// </summary>
    ReconcileDefinition = 16,

    /// <summary>
    ///     均衡现有分屏的两个子节点。
    /// </summary>
    EqualizeSplit = 17,
}
