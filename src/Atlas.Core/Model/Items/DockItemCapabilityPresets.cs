namespace Atlas.Core.Items;

/// <summary>
/// Provides the capability combinations defined by the Atlas domain model.
/// </summary>
public static class DockItemCapabilityPresets
{
    /// <summary>
    /// Gets the default capabilities for a tool item.
    /// </summary>
    public static DockItemCapabilities Tool =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    /// Gets the capabilities for a permanent tool item.
    /// </summary>
    public static DockItemCapabilities PermanentTool =>
        DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    /// <summary>
    /// Gets the default capabilities for a document item.
    /// </summary>
    public static DockItemCapabilities Document =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    /// Gets the capabilities for a preview document item.
    /// </summary>
    public static DockItemCapabilities PreviewDocument =>
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove;

    /// <summary>
    /// Gets the conservative capabilities used by unresolved content.
    /// </summary>
    public static DockItemCapabilities Placeholder => DockItemCapabilities.None;
}
