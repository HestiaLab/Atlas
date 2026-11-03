namespace Atlas.Core.Placement;

/// <summary>
///     标识浮动窗口取消浮动时要还原的原始 placement 种类。
/// </summary>
public enum FloatingReturnKind
{
    /// <summary>
    ///     还原到 Layout Tree 中的工具 Group。
    /// </summary>
    Group = 0,

    /// <summary>
    ///     还原到自动隐藏条。
    /// </summary>
    AutoHide = 1,
}
