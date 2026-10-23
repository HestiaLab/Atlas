namespace Atlas.Core.Items;

/// <summary>
///     Capability combination presets built into the domain model: the default capability sets for tool
///     items, permanent tool items, document items, preview document items, and placeholder items.
/// </summary>
public static class DockItemCapabilityPresets
{
    /// <summary>
    ///     Standard tool item: can be closed, moved, and auto-hidden.
    /// </summary>
    public static DockItemCapabilities Tool =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     Permanent tool item: cannot be closed and can only be moved and auto-hidden.
    /// </summary>
    public static DockItemCapabilities PermanentTool =>
        DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    ///     Standard document item: can be closed, moved, split, and pinned.
    /// </summary>
    public static DockItemCapabilities Document =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    ///     Preview document item: can be closed and moved, but cannot be split or pinned.
    /// </summary>
    public static DockItemCapabilities PreviewDocument =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove;

    /// <summary>
    ///     Placeholder item: conservative capabilities for unresolved content, exposing no operations.
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
