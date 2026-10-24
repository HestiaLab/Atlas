namespace Atlas.Core.Items;

/// <summary>
///     声明停靠条目固有支持的用户操作标志；组合必须与条目种类匹配（工具条目
///     与文档条目各有允许子集），并在构造 <c>DockItem</c> 时校验。
/// </summary>
[Flags]
public enum DockItemCapabilities
{
    /// <summary>
    ///     条目不暴露任何可变用户能力。
    /// </summary>
    None = 0,

    /// <summary>
    ///     条目可关闭。
    /// </summary>
    CanClose = 1 << 0,

    /// <summary>
    ///     条目可移动。
    /// </summary>
    CanMove = 1 << 1,

    /// <summary>
    ///     工具条目可放入自动隐藏条（仅工具条目）。
    /// </summary>
    CanAutoHide = 1 << 2,

    /// <summary>
    ///     文档条目可创建或进入分割的文档 Group（仅文档条目）。
    /// </summary>
    CanSplit = 1 << 3,

    /// <summary>
    ///     文档条目可固定（仅文档条目）。
    /// </summary>
    CanPinDocument = 1 << 4,
}
