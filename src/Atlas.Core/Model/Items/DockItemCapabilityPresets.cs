namespace Atlas.Core.Items;

/// <summary>
///     领域模型内置的能力组合预设：工具项、常驻工具项、文档项、
///     预览文档项与占位项的默认能力集。
/// </summary>
public static class DockItemCapabilityPresets
{
    /// <summary>
    ///     标准工具项：可关闭、可移动、可自动隐藏。
    /// </summary>
    public static DockItemCapabilities Tool =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     常驻工具项：不可关闭，仅可移动、可自动隐藏。
    /// </summary>
    public static DockItemCapabilities PermanentTool =>
        DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     标准文档项：可关闭、可移动、可拆分、可固定。
    /// </summary>
    public static DockItemCapabilities Document =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    ///     预览文档项：可关闭、可移动，不可拆分或固定。
    /// </summary>
    public static DockItemCapabilities PreviewDocument =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove;

    /// <summary>
    ///     占位项：未解析内容使用的保守能力，不暴露任何操作。
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
