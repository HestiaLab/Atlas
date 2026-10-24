namespace Atlas.Core.Placement;

/// <summary>
///     标识逻辑工具栏的已提交可见性。
/// </summary>
public enum ToolBarVisibility
{
    /// <summary>
    ///     工具栏显示正常内容。
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     工具栏已折叠，仅隐藏内容，状态保留以便恢复。
    /// </summary>
    Collapsed = 1,
}
