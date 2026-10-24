namespace Atlas.Core.Operations;

/// <summary>
///     标识分割尺寸调整的输入来源；它决定调整是否参与撤销历史合并。
/// </summary>
public enum ResizeSplitInputKind
{
    /// <summary>
    ///     指针拖拽结束时的最终提交；不参与历史合并。
    /// </summary>
    Pointer = 0,

    /// <summary>
    ///     键盘自动重复中的单步：窗口内对同一分割的连续键盘调整合并为一条撤销
    ///     记录。
    /// </summary>
    Keyboard = 1,
}
