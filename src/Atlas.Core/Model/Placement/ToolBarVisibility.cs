namespace Atlas.Core.Placement;

/// <summary>
///     标识逻辑工具栏的已提交可见性。
/// </summary>
public enum ToolBarVisibility
{
    /// <summary>
    ///     工具栏显示其正常内容。
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     工具栏折叠，仅隐藏内容，状态保留可恢复。
    /// </summary>
    Collapsed = 1,
}
