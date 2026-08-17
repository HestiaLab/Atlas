namespace Atlas.Core.Operations;

/// <summary>
///     标识分屏尺寸调整的输入来源；决定调整是否参与撤销历史合并。
/// </summary>
public enum ResizeSplitInputKind
{
    /// <summary>
    ///     指针拖拽结束时的最终提交，不参与历史合并。
    /// </summary>
    Pointer = 0,

    /// <summary>
    ///     键盘连发中的单步调整：窗口期内同分屏的连续键盘调整合并为一条撤销记录。
    /// </summary>
    Keyboard = 1,
}
