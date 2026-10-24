namespace Atlas.Core.Persistence;

/// <summary>
///     把持久化条目标识符映射为恢复后的标识符。
/// </summary>
public sealed class IdentityRemap
{
    /// <summary>
    ///     创建重映射条目；两个标识符都必须非默认且不同，原因必须是已定义的
    ///     枚举值。
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
    ///     获取持久化标识符。
    /// </summary>
    public DockItemId OldItemId { get; }

    /// <summary>
    ///     获取恢复后的标识符。
    /// </summary>
    public DockItemId NewItemId { get; }

    /// <summary>
    ///     获取重映射原因。
    /// </summary>
    public IdentityRemapReason Reason { get; }
}
