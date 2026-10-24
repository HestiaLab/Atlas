namespace Atlas.Core.Operations;

/// <summary>
///     标识稳定的 Atlas 工作区操作种类。
/// </summary>
public enum LayoutOperationKind
{
    /// <summary>
    ///     打开条目。
    /// </summary>
    OpenItem = 0,

    /// <summary>
    ///     关闭条目。
    /// </summary>
    CloseItem = 1,

    /// <summary>
    ///     在所属 Group 内选中条目。
    /// </summary>
    SelectItem = 2,

    /// <summary>
    ///     把条目移动到不同放置位置。
    /// </summary>
    MoveItem = 3,

    /// <summary>
    ///     在当前放置位置内重排条目。
    /// </summary>
    ReorderItem = 4,

    /// <summary>
    ///     为条目创建分割放置。
    /// </summary>
    SplitItem = 5,

    /// <summary>
    ///     调整现有分割的尺寸。
    /// </summary>
    ResizeSplit = 6,

    /// <summary>
    ///     改变 Group 的可见性。
    /// </summary>
    SetGroupVisibility = 7,

    /// <summary>
    ///     保留；尚未实现操作或规划器。
    /// </summary>
    SetAutoHide = 8,

    /// <summary>
    ///     保留；尚未实现操作或规划器。
    /// </summary>
    RestoreAutoHiddenItem = 9,

    /// <summary>
    ///     更新条目的呈现元数据。
    /// </summary>
    UpdateItemPresentation = 10,

    /// <summary>
    ///     保留；尚未实现操作或规划器。
    /// </summary>
    ReplaceContentReference = 11,

    /// <summary>
    ///     把工作区恢复至创建时的初始布局定义。
    /// </summary>
    ResetWorkspace = 12,

    /// <summary>
    ///     恢复持久化的工作区状态。
    /// </summary>
    RestoreWorkspace = 13,

    /// <summary>
    ///     改变某一逻辑侧的工具栏可见性。
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
    ///     均分现有分割的两个子项。
    /// </summary>
    EqualizeSplit = 17,

    /// <summary>
    ///     持久化失败的错误上下文；不是可调用操作。
    /// </summary>
    Persistence = 20,

    /// <summary>
    ///     工作区工厂失败的错误上下文；不是可调用操作。
    /// </summary>
    Factory = 21,

    /// <summary>
    ///     历史失败的错误上下文；不是可调用操作。
    /// </summary>
    History = 22,
}
