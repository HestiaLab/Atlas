namespace Atlas.Core.Items;

/// <summary>
///     Declares the user-operation flags a dock item inherently supports;
///     the combination must match the item kind (tool items and document items each have an allowed
///     subset) and is validated when constructing <c>DockItem</c>.
/// </summary>
[Flags]
public enum DockItemCapabilities
{
    /// <summary>
    ///     The item exposes no mutable user capability.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The item can be closed.
    /// </summary>
    CanClose = 1 << 0,

    /// <summary>
    ///     The item can be moved.
    /// </summary>
    CanMove = 1 << 1,

    /// <summary>
    ///     The tool item can be placed in an auto-hide strip (tool items only).
    /// </summary>
    CanAutoHide = 1 << 2,

    /// <summary>
    ///     The document item can create or enter a split document group (document items only).
    /// </summary>
    CanSplit = 1 << 3,

    /// <summary>
    ///     The document item can be pinned (document items only).
    /// </summary>
    CanPinDocument = 1 << 4,
}
