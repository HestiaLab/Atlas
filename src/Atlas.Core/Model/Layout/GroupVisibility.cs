namespace Atlas.Core.Layout;

/// <summary>
///     标识 Group 的已提交可见性。
/// </summary>
public enum GroupVisibility
{
    /// <summary>
    ///     Group 参与正常内容布局。
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     Group 保留在树中但不占用正常内容空间；只有工具 Group 可以折叠
    ///     （文档 Group 会被构造校验拒绝）。
    /// </summary>
    Collapsed = 1,
}
