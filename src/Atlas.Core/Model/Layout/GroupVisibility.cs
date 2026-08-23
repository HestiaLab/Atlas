namespace Atlas.Core.Layout;

/// <summary>
///     标识组的已提交可见性。
/// </summary>
public enum GroupVisibility
{
    /// <summary>
    ///     组参与正常的内容布局。
    /// </summary>
    Expanded = 0,

    /// <summary>
    ///     组保留在树中但不占用正常内容空间；
    ///     仅工具组允许折叠（文档组构造校验禁止）。
    /// </summary>
    Collapsed = 1,
}
