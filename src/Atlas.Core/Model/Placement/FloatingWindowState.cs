namespace Atlas.Core.Placement;

/// <summary>
///     一个 Tool Item 的已提交浮动窗口状态：几何与取消浮动时的返回书签。
/// </summary>
public sealed class FloatingWindowState : IEquatable<FloatingWindowState>
{
    /// <summary>
    ///     构造浮窗状态；条目标识、几何与返回书签都不得为默认值。
    /// </summary>
    /// <param name="itemId">浮出的工具条目标识。</param>
    /// <param name="geometry">宿主相对几何。</param>
    /// <param name="returnPlacement">取消浮动时的返回书签。</param>
    public FloatingWindowState(
        DockItemId itemId,
        FloatingWindowGeometry geometry,
        FloatingReturnPlacement returnPlacement)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "A floating-window item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (geometry.IsDefault)
        {
            throw new ArgumentException(
                "A floating window requires a geometry.",
                nameof(geometry));
        }

        if (returnPlacement.IsDefault)
        {
            throw new ArgumentException(
                "A floating window requires a return placement.",
                nameof(returnPlacement));
        }

        ItemId = itemId;
        Geometry = geometry;
        ReturnPlacement = returnPlacement;
    }

    /// <summary>
    ///     浮出的工具条目标识。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     宿主相对几何。
    /// </summary>
    public FloatingWindowGeometry Geometry { get; }

    /// <summary>
    ///     取消浮动时的返回书签。
    /// </summary>
    public FloatingReturnPlacement ReturnPlacement { get; }

    /// <inheritdoc />
    public bool Equals(FloatingWindowState? other)
    {
        return other is not null
            && ItemId == other.ItemId
            && Geometry.Equals(other.Geometry)
            && ReturnPlacement.Equals(other.ReturnPlacement);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FloatingWindowState other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(ItemId, Geometry, ReturnPlacement);
    }
}
