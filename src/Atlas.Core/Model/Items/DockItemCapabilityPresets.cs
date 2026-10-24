namespace Atlas.Core.Items;

/// <summary>
///     领域模型内置的能力组合预设：工具条目、永久工具条目、文档条目、预览文档
///     条目与占位条目的默认能力集。
/// </summary>
public static class DockItemCapabilityPresets
{
    /// <summary>
    ///     标准工具条目：可关闭、移动与自动隐藏。
    /// </summary>
    public static DockItemCapabilities Tool =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     永久工具条目：不可关闭，只能移动与自动隐藏。
    /// </summary>
    public static DockItemCapabilities PermanentTool =>
        DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     标准文档条目：可关闭、移动、分割与固定。
    /// </summary>
    public static DockItemCapabilities Document =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    ///     预览文档条目：可关闭与移动，但不能分割或固定。
    /// </summary>
    public static DockItemCapabilities PreviewDocument =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove;

    /// <summary>
    ///     占位条目：用于未解析内容的保守能力集，不暴露任何操作。
    /// </summary>
    public static DockItemCapabilities Placeholder => DockItemCapabilities.None;

    /// <summary>
    ///     所有已知能力的并集；DockItem 构造时据此拒绝未知能力位。
    /// </summary>
    internal static DockItemCapabilities KnownCapabilities => Tool | Document;

    /// <summary>
    ///     返回指定项种类允许的能力子集。
    /// </summary>
    internal static DockItemCapabilities AllowedFor(DockItemKind kind)
    {
        return kind == DockItemKind.Tool ? Tool : Document;
    }
}
