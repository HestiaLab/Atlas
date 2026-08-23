namespace Atlas.Core.Persistence;

/// <summary>
///     将一个持久化的项标识映射到其恢复后的标识。
/// </summary>
public sealed class IdentityRemap
{
    /// <summary>
    ///     构造重映射条目；两个标识必须非默认且互不相同，原因必须为已定义枚举值。
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
    ///     获取持久化的标识。
    /// </summary>
    public DockItemId OldItemId { get; }

    /// <summary>
    ///     获取恢复后的标识。
    /// </summary>
    public DockItemId NewItemId { get; }

    /// <summary>
    ///     获取重映射原因。
    /// </summary>
    public IdentityRemapReason Reason { get; }
}
