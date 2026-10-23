namespace Atlas.Core.Persistence;

/// <summary>
///     Maps a persisted item identifier to its restored identifier.
/// </summary>
public sealed class IdentityRemap
{
    /// <summary>
    ///     Creates a remap entry; both identifiers must be non-default and distinct,
    ///     and the reason must be a defined enumeration value.
    /// </summary>
    public IdentityRemap(
        DockItemId oldItemId,
        DockItemId newItemId,
        IdentityRemapReason reason)
    {
        if (oldItemId == default || newItemId == default || oldItemId == newItemId)
        {
            throw new ArgumentException("A remap requires two distinct non-default identities.");
        }

        if (!Enum.IsDefined(typeof(IdentityRemapReason), reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown remap reason.");
        }

        OldItemId = oldItemId;
        NewItemId = newItemId;
        Reason = reason;
    }

    /// <summary>
    ///     Gets the persisted identifier.
    /// </summary>
    public DockItemId OldItemId { get; }

    /// <summary>
    ///     Gets the restored identifier.
    /// </summary>
    public DockItemId NewItemId { get; }

    /// <summary>
    ///     Gets the remap reason.
    /// </summary>
    public IdentityRemapReason Reason { get; }
}
