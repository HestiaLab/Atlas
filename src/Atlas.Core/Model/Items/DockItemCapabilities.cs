namespace Atlas.Core.Items;

/// <summary>
///     声明停靠项固有支持的用户操作位；
///     组合必须匹配项种类（工具项与文档项各有允许子集），构造 <c>DockItem</c> 时校验。
/// </summary>
[Flags]
public enum DockItemCapabilities
{
    /// <summary>
    ///     该项不暴露任何可变的用户能力。
    /// </summary>
    None = 0,

    /// <summary>
    ///     该项可以关闭。
    /// </summary>
    CanClose = 1 << 0,

    /// <summary>
    ///     该项可以移动。
    /// </summary>
    CanMove = 1 << 1,

    /// <summary>
    ///     工具项可以放置在自动隐藏条中（仅工具项允许）。
    /// </summary>
    CanAutoHide = 1 << 2,

    /// <summary>
    ///     文档项可以创建或进入分屏文档组（仅文档项允许）。
    /// </summary>
    CanSplit = 1 << 3,

    /// <summary>
    ///     文档项可以固定（仅文档项允许）。
    /// </summary>
    CanPinDocument = 1 << 4,
}
