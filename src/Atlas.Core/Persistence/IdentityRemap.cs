namespace Atlas.Core.Persistence;

/// <summary>Maps one persisted item identity to its restored identity.</summary>
public sealed class IdentityRemap
{
    /// <summary>Initializes an identity remap entry.</summary>
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

    /// <summary>Gets the persisted identity.</summary>
    public DockItemId OldItemId { get; }

    /// <summary>Gets the restored identity.</summary>
    public DockItemId NewItemId { get; }

    /// <summary>Gets the remap reason.</summary>
    public IdentityRemapReason Reason { get; }
}
