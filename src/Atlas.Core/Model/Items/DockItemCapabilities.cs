namespace Atlas.Core.Items;

/// <summary>
/// Describes the stable operations intrinsically supported by a dock item.
/// </summary>
[Flags]
public enum DockItemCapabilities
{
    /// <summary>
    /// The item exposes no mutable user capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// The item can be closed.
    /// </summary>
    CanClose = 1 << 0,

    /// <summary>
    /// The item can be moved.
    /// </summary>
    CanMove = 1 << 1,

    /// <summary>
    /// The tool item can be placed in an auto-hide strip.
    /// </summary>
    CanAutoHide = 1 << 2,

    /// <summary>
    /// The document item can create or enter a split document group.
    /// </summary>
    CanSplit = 1 << 3,

    /// <summary>
    /// The document item can be pinned.
    /// </summary>
    CanPinDocument = 1 << 4,
}
